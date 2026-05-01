// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Html;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Span-based UTF-8 inline parser and HTML renderer.
/// </summary>
/// <remarks>
/// Walks one block's inner UTF-8 byte slice and emits HTML straight to
/// an <see cref="IBufferWriter{T}"/>. No string materialisation; no
/// allocations on the happy path. Supports the inline subset every
/// markdown document uses heavily:
/// <list type="bullet">
/// <item>code spans (<c>`code`</c> / <c>``cod`e``</c>),</item>
/// <item>strong (<c>**text**</c>) and emphasis (<c>*text*</c>, <c>_text_</c>),</item>
/// <item>inline links (<c>[label](href)</c>),</item>
/// <item>autolinks (<c>&lt;https://...&gt;</c>),</item>
/// <item>hard breaks (two trailing spaces before a newline).</item>
/// </list>
/// The full emphasis-stack algorithm and reference-style links land
/// alongside the link-reference-definition pass; this implementation
/// is the line-of-sight version that handles every nesting case
/// real-world docs hit and falls back to escaped text on the rest.
/// </remarks>
public static class InlineRenderer
{
    /// <summary>Backslash byte (escape marker).</summary>
    private const byte Backslash = (byte)'\\';

    /// <summary>Backtick byte (code span fence).</summary>
    private const byte Backtick = (byte)'`';

    /// <summary>Asterisk byte (emphasis marker).</summary>
    private const byte Star = (byte)'*';

    /// <summary>Underscore byte (emphasis marker).</summary>
    private const byte Underscore = (byte)'_';

    /// <summary>Open-bracket byte (link label start).</summary>
    private const byte OpenBracket = (byte)'[';

    /// <summary>Less-than byte (autolink open).</summary>
    private const byte Lt = (byte)'<';

    /// <summary>Space byte (potential hard-break leader).</summary>
    private const byte Sp = (byte)' ';

    /// <summary>
    /// Renders the inline content of <paramref name="source"/> to
    /// <paramref name="writer"/>.
    /// </summary>
    /// <param name="source">UTF-8 inner text of the block.</param>
    /// <param name="writer">UTF-8 HTML sink.</param>
    public static void Render(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var pos = 0;
        var pendingTextStart = 0;
        while (pos < source.Length)
        {
            var b = source[pos];
            var handled = TryHandleSpecial(source, ref pos, ref pendingTextStart, b, writer);
            if (!handled)
            {
                pos++;
            }
        }

        FlushText(source, pendingTextStart, source.Length, writer);
    }

    /// <summary>Flushes pending plain text up to (but not including) <paramref name="end"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Inclusive start of the run.</param>
    /// <param name="end">Exclusive end of the run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    internal static void FlushText(ReadOnlySpan<byte> source, int start, int end, IBufferWriter<byte> writer)
    {
        if (end <= start)
        {
            return;
        }

        HtmlEscape.EscapeText(source[start..end], writer);
    }

    /// <summary>Dispatches a single byte to its inline handler.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past handled construct on success.</param>
    /// <param name="pendingTextStart">Start of the pending escaped-text run.</param>
    /// <param name="b">Byte at <paramref name="pos"/>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when the byte opened a known inline construct.</returns>
    private static bool TryHandleSpecial(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        byte b,
        IBufferWriter<byte> writer) =>
        b switch
        {
            Backslash => InlineEscape.TryHandle(source, ref pos, ref pendingTextStart, writer),
            Backtick => CodeSpan.TryHandle(source, ref pos, ref pendingTextStart, writer),
            Star or Underscore => Emphasis.TryHandle(source, ref pos, ref pendingTextStart, writer),
            OpenBracket => LinkSpan.TryHandle(source, ref pos, ref pendingTextStart, writer),
            Lt => AutoLink.TryHandle(source, ref pos, ref pendingTextStart, writer)
                || RawHtml.TryHandle(source, ref pos, ref pendingTextStart, writer),
            Sp => HardBreak.TryHandle(source, ref pos, ref pendingTextStart, writer),
            _ => false,
        };
}
