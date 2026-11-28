// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Highlight;

/// <summary>Parses 1-based line numbers and <c>start-end</c> ranges from <c>hl_lines</c> and <c>{...}</c> fence attributes.</summary>
internal static class LineHighlightSet
{
    /// <summary>Upper bound on expanded range spans to guard against pathological inputs.</summary>
    private const int MaxExpandedLine = 10_000;

    /// <summary>Parses space- or comma-separated line numbers and ranges from <paramref name="spec"/> into <paramref name="lines"/>.</summary>
    /// <param name="spec">UTF-8 range specification (for example <c>"2 4-6"</c> or <c>"1,3-5"</c>).</param>
    /// <param name="lines">Destination set allocated lazily when at least one valid line number is parsed.</param>
    internal static void ParseInto(ReadOnlySpan<byte> spec, ref HashSet<int>? lines)
    {
        var cursor = 0;
        while (cursor < spec.Length)
        {
            if (IsSeparator(spec[cursor]))
            {
                cursor++;
                continue;
            }

            var tokenStart = cursor;
            while (cursor < spec.Length && !IsSeparator(spec[cursor]))
            {
                cursor++;
            }

            AddToken(spec[tokenStart..cursor], ref lines);
        }
    }

    /// <summary>Parses a positive 1-based starting line number from a <c>linenums</c> fence value.</summary>
    /// <param name="value">UTF-8 value bytes (for example <c>"1"</c> or <c>"10"</c>).</param>
    /// <returns>Parsed positive start line, or <c>1</c> when non-numeric or non-positive.</returns>
    internal static int ParseStartLine(ReadOnlySpan<byte> value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1
            ? Math.Min(parsed, MaxExpandedLine)
            : 1;

    /// <summary>Checks whether <paramref name="b"/> separates range tokens.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns><c>true</c> for spaces, tabs, or commas.</returns>
    private static bool IsSeparator(byte b) =>
        b is (byte)' ' or (byte)'\t' or (byte)',';

    /// <summary>Parses a single number or <c>start-end</c> token and records matching 1-based line numbers.</summary>
    /// <param name="token">Single range token.</param>
    /// <param name="lines">Destination set.</param>
    private static void AddToken(ReadOnlySpan<byte> token, ref HashSet<int>? lines)
    {
        var dash = token.IndexOf((byte)'-');
        if (dash < 0)
        {
            if (TryParseLineNumber(token, out var single))
            {
                _ = (lines ??= []).Add(single);
            }

            return;
        }

        if (!TryParseLineNumber(token[..dash], out var start)
            || !TryParseLineNumber(token[(dash + 1)..], out var end)
            || end < start)
        {
            return;
        }

        var set = lines ??= [];
        for (var line = start; line <= end; line++)
        {
            _ = set.Add(line);
        }
    }

    /// <summary>Parses a bounded 1-based line number.</summary>
    /// <param name="bytes">ASCII digit span.</param>
    /// <param name="lineNumber">Parsed line number.</param>
    /// <returns><c>true</c> when within <c>1..MaxExpandedLine</c>.</returns>
    private static bool TryParseLineNumber(ReadOnlySpan<byte> bytes, out int lineNumber)
    {
        if (int.TryParse(bytes, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value is >= 1 and <= MaxExpandedLine)
        {
            lineNumber = value;
            return true;
        }

        lineNumber = 0;
        return false;
    }
}
