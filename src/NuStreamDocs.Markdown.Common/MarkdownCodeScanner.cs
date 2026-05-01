// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// Stateless byte-level scanning helpers used by every preprocessor
/// that needs to walk markdown source while passing fenced-code and
/// inline-code regions through verbatim.
/// </summary>
/// <remarks>
/// These were duplicated across <c>NuStreamDocs.Emoji</c>,
/// <c>NuStreamDocs.Keys</c>, <c>NuStreamDocs.Abbr</c>,
/// <c>NuStreamDocs.Arithmatex</c>, <c>NuStreamDocs.SmartSymbols</c>,
/// <c>NuStreamDocs.MagicLink</c>, the <c>NuStreamDocs.MarkdownExtensions</c>
/// caret/tilde, critic-markup and inline-hilite rewriters, and the
/// theme-shared icon-shortcode rewriter. One copy now.
/// </remarks>
public static class MarkdownCodeScanner
{
    /// <summary>Gets the fenced-code triple-backtick marker.</summary>
    public static ReadOnlySpan<byte> Backticks => "```"u8;

    /// <summary>Gets the fenced-code triple-tilde marker.</summary>
    public static ReadOnlySpan<byte> Tildes => "~~~"u8;

    /// <summary>Returns true when <paramref name="offset"/> is the start of a line.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <returns>True when at line start.</returns>
    public static bool AtLineStart(ReadOnlySpan<byte> source, int offset) =>
        offset is 0 || source[offset - 1] == (byte)'\n';

    /// <summary>Returns the byte offset just past the next newline (or <paramref name="source"/>'s length when absent).</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Offset to start scanning from.</param>
    /// <returns>Inclusive line-end offset.</returns>
    public static int LineEnd(ReadOnlySpan<byte> source, int offset)
    {
        var rel = source[offset..].IndexOf((byte)'\n');
        return rel < 0 ? source.Length : offset + rel + 1;
    }

    /// <summary>Consumes a fenced-code block opening at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Line-start offset of the opening fence.</param>
    /// <param name="fenceEnd">Exclusive end of the consumed block on success; the source length when the closing fence is missing.</param>
    /// <returns>True when a fenced block was consumed.</returns>
    public static bool TryConsumeFence(ReadOnlySpan<byte> source, int offset, out int fenceEnd)
    {
        fenceEnd = 0;
        ReadOnlySpan<byte> marker;
        if (offset + Backticks.Length <= source.Length && source[offset..].StartsWith(Backticks))
        {
            marker = Backticks;
        }
        else if (offset + Tildes.Length <= source.Length && source[offset..].StartsWith(Tildes))
        {
            marker = Tildes;
        }
        else
        {
            return false;
        }

        var p = LineEnd(source, offset);
        while (p < source.Length)
        {
            if (AtLineStart(source, p)
                && p + marker.Length <= source.Length
                && source[p..].StartsWith(marker))
            {
                p = LineEnd(source, p);
                fenceEnd = p;
                return true;
            }

            p = LineEnd(source, p);
        }

        fenceEnd = source.Length;
        return true;
    }

    /// <summary>Consumes an inline-code span starting at a backtick run.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Offset of the leading backtick.</param>
    /// <returns>Exclusive end of the inline-code span; <paramref name="offset"/> + run-length when no matching close was found.</returns>
    public static int ConsumeInlineCode(ReadOnlySpan<byte> source, int offset)
    {
        var run = 0;
        while (offset + run < source.Length && source[offset + run] is (byte)'`')
        {
            run++;
        }

        var p = offset + run;
        while (p < source.Length)
        {
            if (source[p] is (byte)'`')
            {
                var closeRun = 0;
                while (p + closeRun < source.Length && source[p + closeRun] is (byte)'`')
                {
                    closeRun++;
                }

                if (closeRun == run)
                {
                    return p + closeRun;
                }

                p += closeRun;
                continue;
            }

            p++;
        }

        return offset + run;
    }
}
