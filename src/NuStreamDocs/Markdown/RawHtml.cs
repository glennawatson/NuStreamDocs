// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Raw-HTML pass-through for inline content per CommonMark §6.6.
/// </summary>
/// <remarks>
/// Detects open tags, closing tags, self-closing tags, and HTML comments
/// inline within a paragraph or other block, and emits them verbatim
/// (no entity escaping). Falls back to <c>false</c> when the byte at the
/// cursor doesn't begin a recognized raw-HTML construct, leaving the
/// caller to treat <c>&lt;</c> as literal text.
/// <para>
/// Implemented as a small hand-rolled scanner — no regex, no
/// allocations on the happy path. The recognized forms are:
/// <list type="bullet">
/// <item><c>&lt;tag attr="val"&gt;</c> open tag,</item>
/// <item><c>&lt;tag /&gt;</c> self-closing tag,</item>
/// <item><c>&lt;/tag&gt;</c> close tag,</item>
/// <item><c>&lt;!-- comment --&gt;</c> HTML comment.</item>
/// </list>
/// Processing instructions (<c>&lt;?...?&gt;</c>), CDATA sections
/// and declarations are intentionally omitted — they don't appear in
/// real-world markdown the way the four forms above do, and keeping
/// the scanner narrow keeps the per-byte cost low on the inline hot path.
/// </para>
/// </remarks>
internal static class RawHtml
{
    /// <summary>Greater-than byte.</summary>
    private const byte Gt = (byte)'>';

    /// <summary>Forward-slash byte.</summary>
    private const byte Slash = (byte)'/';

    /// <summary>Bang byte (declaration / comment leader).</summary>
    private const byte Bang = (byte)'!';

    /// <summary>Hyphen byte (HTML-comment delimiter byte).</summary>
    private const byte Hyphen = (byte)'-';

    /// <summary>Minimum length of a complete HTML comment <c>&lt;!---&gt;</c> through to its closing <c>--&gt;</c>.</summary>
    private const int MinHtmlCommentLength = 7;

    /// <summary>Bytes consumed by the closing <c>--&gt;</c> of an HTML comment.</summary>
    private const int CommentCloseLength = 3;

    /// <summary>Offset into <c>&lt;!--</c> at which the first <c>-</c> sits.</summary>
    private const int CommentFirstHyphenOffset = 2;

    /// <summary>Offset into <c>&lt;!--</c> at which the second <c>-</c> sits.</summary>
    private const int CommentSecondHyphenOffset = 3;

    /// <summary>Length of the comment opener <c>&lt;!--</c>.</summary>
    private const int CommentOpenLength = 4;

    /// <summary>Offset of the second <c>-</c> from the cursor while scanning for the closing <c>--&gt;</c>.</summary>
    private const int CommentCloseSecondHyphenOffset = 1;

    /// <summary>Offset of the closing <c>&gt;</c> from the cursor while scanning for the closing <c>--&gt;</c>.</summary>
    private const int CommentCloseGtOffset = 2;

    /// <summary>Tries to match a raw-HTML construct starting at <paramref name="pos"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the construct on success.</param>
    /// <param name="pendingTextStart">Start of the pending plain-text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when a raw-HTML construct was emitted.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        if (pos + 1 >= source.Length)
        {
            return false;
        }

        var second = source[pos + 1];
        var end = second switch
        {
            Slash => FindCloseTagEnd(source, pos),
            Bang => FindCommentEnd(source, pos),
            _ => FindOpenTagEnd(source, pos),
        };

        if (end < 0)
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, pos, writer);
        Write(source[pos..end], writer);
        pos = end;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Locates the end of an open / self-closing tag starting at <paramref name="start"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Cursor at the leading <c>&lt;</c>.</param>
    /// <returns>Exclusive end offset, or -1 when no tag is recognized.</returns>
    private static int FindOpenTagEnd(ReadOnlySpan<byte> source, int start)
    {
        var p = start + 1;
        if (p >= source.Length || !IsTagNameStart(source[p]))
        {
            return -1;
        }

        p++;
        while (p < source.Length && IsTagNameByte(source[p]))
        {
            p++;
        }

        // After the tag name we accept any non-newline content up to the closing '>'.
        // CommonMark constrains attribute syntax tightly, but real-world inline HTML
        // matches that grammar in practice; a permissive end-finder is safe because
        // we still bail on newlines (which would mean the tag is malformed/multi-line).
        while (p < source.Length)
        {
            var b = source[p];
            if (b == Gt)
            {
                return p + 1;
            }

            if (b == (byte)'\n')
            {
                return -1;
            }

            p++;
        }

        return -1;
    }

    /// <summary>Locates the end of a close tag starting at <paramref name="start"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Cursor at the leading <c>&lt;</c>.</param>
    /// <returns>Exclusive end offset, or -1 when no close tag is recognized.</returns>
    private static int FindCloseTagEnd(ReadOnlySpan<byte> source, int start)
    {
        var p = start + 2;
        if (p >= source.Length || !IsTagNameStart(source[p]))
        {
            return -1;
        }

        p++;
        while (p < source.Length && IsTagNameByte(source[p]))
        {
            p++;
        }

        // Optional whitespace before '>'.
        while (p < source.Length && source[p] is (byte)' ' or (byte)'\t')
        {
            p++;
        }

        if (p < source.Length && source[p] == Gt)
        {
            return p + 1;
        }

        return -1;
    }

    /// <summary>Locates the end of an HTML comment starting at <paramref name="start"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Cursor at the leading <c>&lt;</c>.</param>
    /// <returns>Exclusive end offset, or -1 when no comment is recognized.</returns>
    private static int FindCommentEnd(ReadOnlySpan<byte> source, int start)
    {
        if (start + MinHtmlCommentLength > source.Length
            || source[start + 1] != Bang
            || source[start + CommentFirstHyphenOffset] != Hyphen
            || source[start + CommentSecondHyphenOffset] != Hyphen)
        {
            return -1;
        }

        for (var p = start + CommentOpenLength; p + CommentCloseLength <= source.Length; p++)
        {
            if (source[p] == Hyphen
                && source[p + CommentCloseSecondHyphenOffset] == Hyphen
                && source[p + CommentCloseGtOffset] == Gt)
            {
                return p + CommentCloseLength;
            }
        }

        return -1;
    }

    /// <summary>True when <paramref name="b"/> may begin a tag name (ASCII letter).</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for letters.</returns>
    private static bool IsTagNameStart(byte b) =>
        b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z';

    /// <summary>True when <paramref name="b"/> may continue a tag name (alphanumeric or hyphen).</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for alphanumerics and hyphen.</returns>
    private static bool IsTagNameByte(byte b) =>
        IsTagNameStart(b)
        || b is >= (byte)'0' and <= (byte)'9'
        || b == Hyphen;

    /// <summary>Bulk-writes <paramref name="bytes"/>.</summary>
    /// <param name="bytes">UTF-8 bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void Write(ReadOnlySpan<byte> bytes, IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }
}
