// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Minimal block-style YAML to UTF-8 JSON converter.
/// </summary>
/// <remarks>
/// Targets the subset of YAML real-world <c>mkdocs.yml</c> files use:
/// nested block mappings, block sequences, quoted and plain scalars,
/// and <c>#</c>-introduced comments. Out of scope: flow style
/// (<c>{a: b}</c>, <c>[a, b]</c>), folded / literal scalars
/// (<c>|</c>, <c>&gt;</c>), anchors / aliases / tags, and explicit
/// document markers. Anything outside the supported subset is treated
/// as a plain string.
/// <para>
/// Conversion is allocation-conscious: the input is consumed as a
/// <see cref="ReadOnlySpan{Byte}"/> and JSON is produced with a
/// caller-supplied <see cref="Utf8JsonWriter"/> — no UTF-16 round
/// trip, no intermediate document model.
/// </para>
/// </remarks>
public static class YamlToJson
{
    /// <summary>Tab byte.</summary>
    private const byte Tab = (byte)'\t';

    /// <summary>Space byte.</summary>
    private const byte Sp = (byte)' ';

    /// <summary>Hyphen byte (sequence-item marker).</summary>
    private const byte Hyphen = (byte)'-';

    /// <summary>Colon byte (key/value separator).</summary>
    private const byte Colon = (byte)':';

    /// <summary>Hash byte (comment marker).</summary>
    private const byte Hash = (byte)'#';

    /// <summary>Indent step used for nested mappings inside an inline sequence item.</summary>
    private const int InlineSequenceMappingIndent = 2;

    /// <summary>Double-quote byte.</summary>
    private const byte DQuote = (byte)'"';

    /// <summary>Single-quote byte.</summary>
    private const byte SQuote = (byte)'\'';

    /// <summary>Carriage-return byte.</summary>
    private const byte Cr = (byte)'\r';

    /// <summary>Line-feed byte.</summary>
    private const byte Lf = (byte)'\n';

    /// <summary>
    /// Converts <paramref name="yaml"/> to JSON written through
    /// <paramref name="json"/>.
    /// </summary>
    /// <param name="yaml">UTF-8 YAML source bytes.</param>
    /// <param name="json">UTF-8 JSON sink.</param>
    public static void Convert(ReadOnlySpan<byte> yaml, Utf8JsonWriter json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var stack = ArrayPool<ContainerFrame>.Shared.Rent(16);
        var depth = OpenRoot(json, stack);

        try
        {
            var pos = 0;
            while (pos < yaml.Length)
            {
                ReadLine(yaml, pos, out var contentEnd, out var nextLine);
                ProcessLine(yaml[pos..contentEnd], json, stack, ref depth);
                pos = nextLine;
            }
        }
        finally
        {
            CloseRemaining(json, stack, depth);
            ArrayPool<ContainerFrame>.Shared.Return(stack, clearArray: true);
        }
    }

    /// <summary>
    /// Streaming variant: converts <paramref name="utf8Stream"/> to JSON
    /// without buffering the whole file in memory.
    /// </summary>
    /// <param name="utf8Stream">UTF-8 YAML source stream.</param>
    /// <param name="json">UTF-8 JSON sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the stream is fully consumed.</returns>
    public static async Task ConvertAsync(Stream utf8Stream, Utf8JsonWriter json, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8Stream);
        ArgumentNullException.ThrowIfNull(json);

        var stack = ArrayPool<ContainerFrame>.Shared.Rent(16);
        var depth = OpenRoot(json, stack);
        using var reader = new Utf8LineReader(utf8Stream, leaveOpen: true);

        try
        {
            var (hasLine, line) = await reader.TryReadLineAsync(cancellationToken).ConfigureAwait(false);
            while (hasLine)
            {
                ProcessLine(line.Span, json, stack, ref depth);
                (hasLine, line) = await reader.TryReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            CloseRemaining(json, stack, depth);
            ArrayPool<ContainerFrame>.Shared.Return(stack, clearArray: true);
        }
    }

    /// <summary>Initializes the synthetic root-mapping frame.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="stack">Container-frame stack.</param>
    /// <returns>Initial stack depth.</returns>
    private static int OpenRoot(Utf8JsonWriter json, ContainerFrame[] stack)
    {
        json.WriteStartObject();
        stack[0] = new(Indent: -1, Kind: ContainerKind.Mapping);
        return 1;
    }

    /// <summary>Reads one line's content end and the start of the next line.</summary>
    /// <param name="yaml">Source buffer.</param>
    /// <param name="pos">Current position.</param>
    /// <param name="contentEnd">Set to the byte after the line content (excludes CR/LF).</param>
    /// <param name="nextLine">Set to the byte after the line terminator.</param>
    private static void ReadLine(ReadOnlySpan<byte> yaml, int pos, out int contentEnd, out int nextLine)
    {
        var lf = yaml[pos..].IndexOf(Lf);
        if (lf < 0)
        {
            contentEnd = yaml.Length;
            nextLine = yaml.Length;
            return;
        }

        var lfAbs = pos + lf;
        nextLine = lfAbs + 1;
        contentEnd = lf > 0 && yaml[lfAbs - 1] == Cr ? lfAbs - 1 : lfAbs;
    }

    /// <summary>Routes one YAML line through the line-shape recognizer.</summary>
    /// <param name="line">Source line (no terminator).</param>
    /// <param name="json">JSON sink.</param>
    /// <param name="stack">Container-frame stack.</param>
    /// <param name="depth">Stack depth.</param>
    private static void ProcessLine(
        ReadOnlySpan<byte> line,
        Utf8JsonWriter json,
        ContainerFrame[] stack,
        ref int depth)
    {
        var indent = LeadingIndent(line);
        var body = line[indent..];
        if (body.IsEmpty || body[0] == Hash)
        {
            return;
        }

        CloseDeeperContainers(json, stack, ref depth, indent);

        if (body[0] == Hyphen && (body.Length == 1 || body[1] is Sp or Tab))
        {
            HandleSequenceItem(body, indent, json, stack, ref depth);
            return;
        }

        HandleMappingEntry(body, indent, json, stack, ref depth);
    }

    /// <summary>Closes containers whose indent is &gt;= <paramref name="lineIndent"/>.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="stack">Container-frame stack.</param>
    /// <param name="depth">Stack depth.</param>
    /// <param name="lineIndent">Indent of the current line.</param>
    private static void CloseDeeperContainers(
        Utf8JsonWriter json,
        ContainerFrame[] stack,
        ref int depth,
        int lineIndent)
    {
        while (depth > 1 && stack[depth - 1].Indent > lineIndent)
        {
            CloseContainer(json, stack[--depth]);
        }
    }

    /// <summary>Handles a sequence-item line (<c>- value</c> or <c>- key: value</c>).</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="indent">Indent column.</param>
    /// <param name="json">JSON sink.</param>
    /// <param name="stack">Container-frame stack.</param>
    /// <param name="depth">Stack depth.</param>
    private static void HandleSequenceItem(
        ReadOnlySpan<byte> body,
        int indent,
        Utf8JsonWriter json,
        ContainerFrame[] stack,
        ref int depth)
    {
        EnsureSequenceContainer(json, stack, ref depth, indent);

        var rest = body[1..].TrimStart(Sp).TrimStart(Tab);
        if (rest.IsEmpty)
        {
            return;
        }

        var colon = FindUnquotedColon(rest);
        if (colon < 0)
        {
            WriteScalar(json, rest);
            return;
        }

        // Inline `- key: value` opens a one-key mapping for this item.
        json.WriteStartObject();
        stack[depth++] = new(Indent: indent + InlineSequenceMappingIndent, Kind: ContainerKind.Mapping);
        WriteMappingEntry(json, rest, colon, stack, ref depth, indent + InlineSequenceMappingIndent);
    }

    /// <summary>Ensures the top of the stack is a sequence at <paramref name="indent"/>.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="stack">Container-frame stack.</param>
    /// <param name="depth">Stack depth.</param>
    /// <param name="indent">Indent column of the sequence-item line.</param>
    private static void EnsureSequenceContainer(
        Utf8JsonWriter json,
        ContainerFrame[] stack,
        ref int depth,
        int indent)
    {
        var top = stack[depth - 1];
        switch (top.Kind)
        {
            case ContainerKind.Sequence when top.Indent == indent:
                return;
            case ContainerKind.Pending:
                {
                    json.WriteStartArray();
                    stack[depth - 1] = new(Indent: indent, Kind: ContainerKind.Sequence);
                    return;
                }

            default:
                {
                    json.WriteStartArray();
                    stack[depth++] = new(Indent: indent, Kind: ContainerKind.Sequence);
                    break;
                }
        }
    }

    /// <summary>Handles a mapping-entry line (<c>key:</c> or <c>key: value</c>).</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="indent">Indent column.</param>
    /// <param name="json">JSON sink.</param>
    /// <param name="stack">Container-frame stack.</param>
    /// <param name="depth">Stack depth.</param>
    private static void HandleMappingEntry(
        ReadOnlySpan<byte> body,
        int indent,
        Utf8JsonWriter json,
        ContainerFrame[] stack,
        ref int depth)
    {
        var colon = FindUnquotedColon(body);
        if (colon < 0)
        {
            return;
        }

        // A child-mapping line under a Pending value means the pending
        // entry is actually a mapping; upgrade the frame in place.
        if (stack[depth - 1].Kind == ContainerKind.Pending)
        {
            json.WriteStartObject();
            stack[depth - 1] = new(Indent: indent, Kind: ContainerKind.Mapping);
        }

        WriteMappingEntry(json, body, colon, stack, ref depth, indent);
    }

    /// <summary>Writes one mapping property and any container the value opens.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="body">Trimmed line bytes.</param>
    /// <param name="colon">Index of the unquoted colon inside <paramref name="body"/>.</param>
    /// <param name="stack">Container-frame stack.</param>
    /// <param name="depth">Stack depth.</param>
    /// <param name="indent">Indent column of the mapping-entry line.</param>
    private static void WriteMappingEntry(
        Utf8JsonWriter json,
        ReadOnlySpan<byte> body,
        int colon,
        ContainerFrame[] stack,
        ref int depth,
        int indent)
    {
        var key = TrimQuotes(body[..colon].TrimEnd(Sp).TrimEnd(Tab));
        var rest = body[(colon + 1)..].TrimStart(Sp).TrimStart(Tab);

        WritePropertyName(json, key);

        if (rest.IsEmpty)
        {
            // Empty value — defer container creation until the next line
            // tells us whether this is a mapping or a sequence.
            stack[depth++] = new(Indent: indent, Kind: ContainerKind.Pending);
            return;
        }

        WriteScalar(json, rest);
    }

    /// <summary>Closes any frames left on the stack at end of input.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="stack">Container-frame stack.</param>
    /// <param name="depth">Stack depth.</param>
    private static void CloseRemaining(Utf8JsonWriter json, ContainerFrame[] stack, int depth)
    {
        while (depth > 0)
        {
            CloseContainer(json, stack[--depth]);
        }
    }

    /// <summary>Closes one container frame.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="frame">Frame to close.</param>
    private static void CloseContainer(Utf8JsonWriter json, in ContainerFrame frame)
    {
        switch (frame.Kind)
        {
            case ContainerKind.Mapping:
            {
                json.WriteEndObject();
                break;
            }

            case ContainerKind.Sequence:
            {
                json.WriteEndArray();
                break;
            }

            case ContainerKind.Pending:
            {
                // No child line opened the container — emit an empty
                // object so the property still has a value.
                json.WriteStartObject();
                json.WriteEndObject();
                break;
            }
        }
    }

    /// <summary>Writes a JSON property name from a YAML key span, handling pending frames.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="key">Key bytes.</param>
    private static void WritePropertyName(Utf8JsonWriter json, ReadOnlySpan<byte> key) =>
        json.WritePropertyName(key);

    /// <summary>Writes a YAML scalar as the appropriate JSON value.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="raw">Scalar bytes (post-trim, possibly quoted).</param>
    private static void WriteScalar(Utf8JsonWriter json, ReadOnlySpan<byte> raw)
    {
        var trimmed = StripTrailingComment(raw);
        if (trimmed.IsEmpty)
        {
            json.WriteNullValue();
            return;
        }

        if (TryWriteQuoted(json, trimmed))
        {
            return;
        }

        if (trimmed.SequenceEqual("true"u8) || trimmed.SequenceEqual("True"u8))
        {
            json.WriteBooleanValue(true);
            return;
        }

        if (trimmed.SequenceEqual("false"u8) || trimmed.SequenceEqual("False"u8))
        {
            json.WriteBooleanValue(false);
            return;
        }

        if (trimmed.SequenceEqual("null"u8) || trimmed.SequenceEqual("~"u8))
        {
            json.WriteNullValue();
            return;
        }

        json.WriteStringValue(trimmed);
    }

    /// <summary>Strips an unquoted trailing <c># comment</c>.</summary>
    /// <param name="raw">Scalar bytes.</param>
    /// <returns>Bytes with any trailing comment removed.</returns>
    private static ReadOnlySpan<byte> StripTrailingComment(ReadOnlySpan<byte> raw)
    {
        if (!raw.IsEmpty && raw[0] is DQuote or SQuote)
        {
            return raw;
        }

        var hash = raw.IndexOf(Hash);
        return hash < 0 ? raw : raw[..hash].TrimEnd(Sp).TrimEnd(Tab);
    }

    /// <summary>Writes a quoted scalar verbatim if the input is wrapped in matching quotes.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="raw">Scalar bytes.</param>
    /// <returns>True when handled.</returns>
    private static bool TryWriteQuoted(Utf8JsonWriter json, ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 2)
        {
            return false;
        }

        var quote = raw[0];
        if (quote is not DQuote and not SQuote || raw[^1] != quote)
        {
            return false;
        }

        json.WriteStringValue(raw[1..^1]);
        return true;
    }

    /// <summary>Removes wrapping quotes from a key span.</summary>
    /// <param name="key">Key bytes.</param>
    /// <returns>Unquoted bytes.</returns>
    private static ReadOnlySpan<byte> TrimQuotes(ReadOnlySpan<byte> key)
    {
        if (key.Length < 2)
        {
            return key;
        }

        var quote = key[0];
        return quote is DQuote or SQuote && key[^1] == quote ? key[1..^1] : key;
    }

    /// <summary>Counts the leading-space-or-tab run.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>Indent column count.</returns>
    private static int LeadingIndent(ReadOnlySpan<byte> line)
    {
        var i = 0;
        while (i < line.Length && line[i] is Sp or Tab)
        {
            i++;
        }

        return i;
    }

    /// <summary>Locates the first colon outside a quoted span.</summary>
    /// <param name="line">UTF-8 bytes.</param>
    /// <returns>Index of the colon, or -1.</returns>
    private static int FindUnquotedColon(ReadOnlySpan<byte> line)
    {
        var inDouble = false;
        var inSingle = false;
        for (var i = 0; i < line.Length; i++)
        {
            switch (line[i])
            {
                case DQuote when !inSingle:
                {
                    inDouble = !inDouble;
                    break;
                }

                case SQuote when !inDouble:
                {
                    inSingle = !inSingle;
                    break;
                }

                case Colon when !inDouble && !inSingle:
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>One container frame on the open-container stack.</summary>
    /// <param name="Indent">Indent column at which this container opens.</param>
    /// <param name="Kind">Container kind.</param>
    private readonly record struct ContainerFrame(int Indent, ContainerKind Kind);
}
