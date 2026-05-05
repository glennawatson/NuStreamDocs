// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Text;
using System.Text.Json;
using NuStreamDocs.Common;

namespace NuStreamDocs.Config.Zensical;

/// <summary>
/// Minimal TOML to UTF-8 JSON converter targeting Zensical's config
/// shape.
/// </summary>
/// <remarks>
/// Span-based; consumes <see cref="ReadOnlySpan{Byte}"/> and writes
/// through a caller-supplied <see cref="Utf8JsonWriter"/> with no
/// UTF-16 round trip. Recognized:
/// <list type="bullet">
/// <item>top-level <c>key = value</c> entries (string, int, bool),</item>
/// <item><c>[table]</c> headers introducing nested objects,</item>
/// <item><c>[table.subtable]</c> dotted-table headers,</item>
/// <item><c>#</c> comments.</item>
/// </list>
/// Out of scope on the first ship: arrays-of-tables (<c>[[x]]</c>),
/// inline tables (<c>{ a = 1 }</c>), arrays, multi-line strings,
/// dates. The shape covers what Zensical's own
/// <c>zensical.toml</c> uses today.
/// </remarks>
public static class TomlToJson
{
    /// <summary>Tab byte.</summary>
    private const byte Tab = (byte)'\t';

    /// <summary>Space byte.</summary>
    private const byte Sp = (byte)' ';

    /// <summary>Hash byte (comment marker).</summary>
    private const byte Hash = (byte)'#';

    /// <summary>Equals byte (key/value separator).</summary>
    private const byte Eq = (byte)'=';

    /// <summary>Dot byte (table-name separator).</summary>
    private const byte Dot = (byte)'.';

    /// <summary>Open-bracket byte (table header start).</summary>
    private const byte OpenBracket = (byte)'[';

    /// <summary>Close-bracket byte (table header end).</summary>
    private const byte CloseBracket = (byte)']';

    /// <summary>Double-quote byte.</summary>
    private const byte DQuote = (byte)'"';

    /// <summary>Single-quote byte.</summary>
    private const byte SQuote = (byte)'\'';

    /// <summary>Carriage-return byte.</summary>
    private const byte Cr = (byte)'\r';

    /// <summary>Line-feed byte.</summary>
    private const byte Lf = (byte)'\n';

    /// <summary>Converts <paramref name="toml"/> to JSON written through <paramref name="json"/>.</summary>
    /// <param name="toml">UTF-8 TOML source bytes.</param>
    /// <param name="json">UTF-8 JSON sink.</param>
    public static void Convert(ReadOnlySpan<byte> toml, Utf8JsonWriter json)
    {
        ArgumentNullException.ThrowIfNull(json);

        json.WriteStartObject();
        var openTables = 0;

        var pos = 0;
        while (pos < toml.Length)
        {
            ReadLine(toml, pos, out var contentEnd, out var nextLine);
            ProcessLine(toml[pos..contentEnd], json, ref openTables);
            pos = nextLine;
        }

        Finalize(json, openTables);
    }

    /// <summary>
    /// Streaming variant: converts <paramref name="utf8Stream"/> to JSON
    /// without buffering the whole file in memory.
    /// </summary>
    /// <param name="utf8Stream">UTF-8 TOML source stream.</param>
    /// <param name="json">UTF-8 JSON sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the stream is fully consumed.</returns>
    public static async Task ConvertAsync(Stream utf8Stream, Utf8JsonWriter json, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8Stream);
        ArgumentNullException.ThrowIfNull(json);

        json.WriteStartObject();
        var openTables = 0;
        using Utf8LineReader reader = new(utf8Stream, leaveOpen: true);

        var (hasLine, line) = await reader.TryReadLineAsync(cancellationToken).ConfigureAwait(false);
        while (hasLine)
        {
            ProcessLine(line.Span, json, ref openTables);
            (hasLine, line) = await reader.TryReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        Finalize(json, openTables);
    }

    /// <summary>Closes any tables left open and writes the root close.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="openTables">Number of currently-open table objects.</param>
    private static void Finalize(Utf8JsonWriter json, int openTables)
    {
        for (var i = 0; i < openTables; i++)
        {
            json.WriteEndObject();
        }

        json.WriteEndObject();
    }

    /// <summary>Reads one line's content end and the start of the next line.</summary>
    /// <param name="toml">Source buffer.</param>
    /// <param name="pos">Current position.</param>
    /// <param name="contentEnd">Byte after the line content (excludes CR/LF).</param>
    /// <param name="nextLine">Byte after the line terminator.</param>
    private static void ReadLine(ReadOnlySpan<byte> toml, int pos, out int contentEnd, out int nextLine)
    {
        var lf = toml[pos..].IndexOf(Lf);
        if (lf < 0)
        {
            contentEnd = toml.Length;
            nextLine = toml.Length;
            return;
        }

        var lfAbs = pos + lf;
        nextLine = lfAbs + 1;
        contentEnd = lf > 0 && toml[lfAbs - 1] == Cr ? lfAbs - 1 : lfAbs;
    }

    /// <summary>Routes one TOML line to a header or key/value handler.</summary>
    /// <param name="line">Line bytes.</param>
    /// <param name="json">JSON sink.</param>
    /// <param name="openTables">Number of currently-open table objects.</param>
    private static void ProcessLine(ReadOnlySpan<byte> line, Utf8JsonWriter json, ref int openTables)
    {
        var trimmed = TrimLeading(line);
        if (trimmed.IsEmpty || trimmed[0] == Hash)
        {
            return;
        }

        if (trimmed[0] == OpenBracket)
        {
            HandleTableHeader(trimmed, json, ref openTables);
            return;
        }

        HandleKeyValue(trimmed, json);
    }

    /// <summary>Handles a <c>[table.path]</c> header.</summary>
    /// <param name="line">Trimmed line, first byte is <c>[</c>.</param>
    /// <param name="json">JSON sink.</param>
    /// <param name="openTables">Number of currently-open table objects.</param>
    private static void HandleTableHeader(ReadOnlySpan<byte> line, Utf8JsonWriter json, ref int openTables)
    {
        var close = line.IndexOf(CloseBracket);
        if (close < 0)
        {
            return;
        }

        // Close any tables left open by the previous header.
        for (var i = 0; i < openTables; i++)
        {
            json.WriteEndObject();
        }

        var path = line[1..close];

        var depth = 0;
        while (!path.IsEmpty)
        {
            var dot = path.IndexOf(Dot);
            var segment = dot < 0 ? path : path[..dot];
            var trimmed = Trim(segment);
            if (!trimmed.IsEmpty)
            {
                json.WritePropertyName(trimmed);
                json.WriteStartObject();
                depth++;
            }

            path = dot < 0 ? default : path[(dot + 1)..];
        }

        openTables = depth;
    }

    /// <summary>Handles a <c>key = value</c> entry.</summary>
    /// <param name="line">Trimmed line.</param>
    /// <param name="json">JSON sink.</param>
    private static void HandleKeyValue(ReadOnlySpan<byte> line, Utf8JsonWriter json)
    {
        var eq = line.IndexOf(Eq);
        if (eq < 0)
        {
            return;
        }

        var key = TrimQuotes(Trim(line[..eq]));
        var value = StripTrailingComment(Trim(line[(eq + 1)..]));

        json.WritePropertyName(key);
        WriteScalar(json, value);
    }

    /// <summary>Writes a TOML scalar as the appropriate JSON value.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="raw">Trimmed value bytes.</param>
    private static void WriteScalar(Utf8JsonWriter json, ReadOnlySpan<byte> raw)
    {
        if (raw.IsEmpty)
        {
            json.WriteNullValue();
            return;
        }

        if (TryWriteQuoted(json, raw))
        {
            return;
        }

        if (raw.SequenceEqual("true"u8))
        {
            json.WriteBooleanValue(true);
            return;
        }

        if (raw.SequenceEqual("false"u8))
        {
            json.WriteBooleanValue(false);
            return;
        }

        if (TryWriteInteger(json, raw))
        {
            return;
        }

        json.WriteStringValue(raw);
    }

    /// <summary>Tries to write a quoted string verbatim.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="raw">Value bytes.</param>
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

    /// <summary>Tries to write a value as an integer.</summary>
    /// <param name="json">JSON sink.</param>
    /// <param name="raw">Value bytes.</param>
    /// <returns>True when handled.</returns>
    private static bool TryWriteInteger(Utf8JsonWriter json, ReadOnlySpan<byte> raw)
    {
        if (!Utf8Parser.TryParse(raw, out long value, out var consumed) || consumed != raw.Length)
        {
            return false;
        }

        json.WriteNumberValue(value);
        return true;
    }

    /// <summary>Strips a trailing unquoted <c># comment</c>.</summary>
    /// <param name="raw">Value bytes.</param>
    /// <returns>Bytes with any comment removed.</returns>
    private static ReadOnlySpan<byte> StripTrailingComment(ReadOnlySpan<byte> raw)
    {
        if (!raw.IsEmpty && raw[0] is DQuote or SQuote)
        {
            return raw;
        }

        var hash = raw.IndexOf(Hash);
        return hash < 0 ? raw : Trim(raw[..hash]);
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

    /// <summary>Trims leading whitespace.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>Trimmed bytes.</returns>
    private static ReadOnlySpan<byte> TrimLeading(ReadOnlySpan<byte> line)
    {
        var i = 0;
        while (i < line.Length && line[i] is Sp or Tab)
        {
            i++;
        }

        return line[i..];
    }

    /// <summary>Trims leading and trailing whitespace.</summary>
    /// <param name="value">UTF-8 bytes.</param>
    /// <returns>Trimmed bytes.</returns>
    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value) =>
        value.TrimStart(Sp).TrimStart(Tab).TrimEnd(Sp).TrimEnd(Tab);
}
