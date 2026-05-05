// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config.DocFx;

/// <summary>Forward-cursor line scanner for the docfx toc.yml subset. Single-pass, allocation-free, byte-only.</summary>
internal ref struct TocLineParser
{
    /// <summary>Bytes consumed by a sequence-marker run (<c>"- "</c> or just <c>"-"</c> at end-of-line).</summary>
    private const int SequenceMarkerLength = 2;

    /// <summary>UTF-8 source bytes.</summary>
    private readonly ReadOnlySpan<byte> _source;

    /// <summary>Current cursor (start of next line).</summary>
    private int _pos;

    /// <summary>Initializes a new instance of the <see cref="TocLineParser"/> struct.</summary>
    /// <param name="source">UTF-8 toc.yml bytes.</param>
    public TocLineParser(ReadOnlySpan<byte> source)
    {
        _source = source;
        _pos = 0;
    }

    /// <summary>Looks at the next non-blank/non-comment line without advancing.</summary>
    /// <param name="line">Decoded line on success.</param>
    /// <returns>True when a line was found.</returns>
    public bool Peek(out TocLine line)
    {
        var snapshot = _pos;
        var ok = TryConsume(out line);
        _pos = snapshot;
        return ok;
    }

    /// <summary>Advances past the next non-blank/non-comment line and returns it.</summary>
    /// <param name="line">Decoded line on success.</param>
    /// <returns>True when a line was consumed.</returns>
    public bool TryConsume(out TocLine line)
    {
        while (_pos < _source.Length)
        {
            var lineEnd = NextLineEnd(_source, _pos);
            var rawLine = _source[_pos..lineEnd];
            _pos = AdvancePastTerminator(_source, lineEnd);

            if (IsBlankOrComment(rawLine))
            {
                continue;
            }

            line = Decode(rawLine);
            return true;
        }

        line = default;
        return false;
    }

    /// <summary>Returns the offset of the line terminator at or after <paramref name="from"/>; <paramref name="source"/>.Length when no terminator remains.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="from">Cursor.</param>
    /// <returns>Offset of <c>\n</c> or end-of-buffer.</returns>
    private static int NextLineEnd(ReadOnlySpan<byte> source, int from)
    {
        var rel = source[from..].IndexOf((byte)'\n');
        return rel < 0 ? source.Length : from + rel;
    }

    /// <summary>Returns the offset of the byte just past the terminator at <paramref name="lineEnd"/>.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="lineEnd">Position of <c>\n</c> or end-of-buffer.</param>
    /// <returns>Cursor for the next line.</returns>
    private static int AdvancePastTerminator(ReadOnlySpan<byte> source, int lineEnd) =>
        lineEnd < source.Length ? lineEnd + 1 : lineEnd;

    /// <summary>Returns true when <paramref name="rawLine"/> is whitespace only or starts with <c>#</c>.</summary>
    /// <param name="rawLine">Line bytes (no terminator).</param>
    /// <returns>True when the line carries no content.</returns>
    private static bool IsBlankOrComment(ReadOnlySpan<byte> rawLine)
    {
        for (var i = 0; i < rawLine.Length; i++)
        {
            var b = rawLine[i];
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r')
            {
                continue;
            }

            return b is (byte)'#';
        }

        return true;
    }

    /// <summary>Decodes a non-empty content line into a <see cref="TocLine"/>.</summary>
    /// <param name="rawLine">Line bytes (no terminator).</param>
    /// <returns>Decoded line.</returns>
    private static TocLine Decode(ReadOnlySpan<byte> rawLine)
    {
        var indent = 0;
        while (indent < rawLine.Length && rawLine[indent] is (byte)' ')
        {
            indent++;
        }

        var body = rawLine[indent..];
        var isSequenceItem = false;
        if (body.Length > 0 && body[0] is (byte)'-' && (body.Length is 1 || body[1] is (byte)' '))
        {
            isSequenceItem = true;
            body = body.Length is 1 ? [] : body[SequenceMarkerLength..];
        }

        var key = SplitKeyValue(body, out var value, out var hasItems);
        return new(indent, isSequenceItem, key, value, hasItems);
    }

    /// <summary>Splits a line body into <c>key: value</c>; recognizes the four supported keys.</summary>
    /// <param name="body">Body bytes (after indent and optional <c>- </c>).</param>
    /// <param name="value">Trimmed and dequoted value span on success; empty otherwise.</param>
    /// <param name="hasItems">True when the key is the <c>items:</c> opener.</param>
    /// <returns>Recognized key kind.</returns>
    private static TocKey SplitKeyValue(ReadOnlySpan<byte> body, out ReadOnlySpan<byte> value, out bool hasItems)
    {
        value = [];
        hasItems = false;

        var colon = body.IndexOf((byte)':');
        if (colon < 0)
        {
            return TocKey.Unknown;
        }

        var keySpan = body[..colon].Trim((byte)' ');
        var valueSpan = colon + 1 < body.Length ? body[(colon + 1)..] : [];
        valueSpan = TrimTrailingComment(valueSpan).Trim((byte)' ').Trim((byte)'\t').Trim((byte)'\r');
        valueSpan = Dequote(valueSpan);
        value = valueSpan;

        if (keySpan.SequenceEqual("name"u8))
        {
            return TocKey.Name;
        }

        if (keySpan.SequenceEqual("href"u8))
        {
            return TocKey.Href;
        }

        if (keySpan.SequenceEqual("homepage"u8))
        {
            return TocKey.Homepage;
        }

        if (keySpan.SequenceEqual("items"u8))
        {
            // `items:` is the opener for an inline sub-sequence; value is empty.
            value = [];
            hasItems = true;
        }

        return TocKey.Unknown;
    }

    /// <summary>Drops everything from a <c>#</c> to end-of-line (inline comment).</summary>
    /// <param name="span">Value bytes.</param>
    /// <returns>Comment-trimmed span.</returns>
    private static ReadOnlySpan<byte> TrimTrailingComment(ReadOnlySpan<byte> span)
    {
        var hash = span.IndexOf((byte)'#');
        return hash < 0 ? span : span[..hash];
    }

    /// <summary>Drops a single matching pair of leading/trailing single- or double-quote bytes.</summary>
    /// <param name="span">Value bytes.</param>
    /// <returns>Unquoted span.</returns>
    private static ReadOnlySpan<byte> Dequote(ReadOnlySpan<byte> span) => span.Length >= 2 && span[0] is (byte)'"' or (byte)'\'' && span[^1] == span[0] ? span[1..^1] : span;
}
