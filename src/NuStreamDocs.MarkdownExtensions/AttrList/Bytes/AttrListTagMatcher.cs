// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Case-insensitive tag-name dispatch for the three attr-list
/// categories: block, paired-inline, void-inline.
/// </summary>
internal static class AttrListTagMatcher
{
    /// <summary>Bytes that terminate a tag name (whitespace, <c>&gt;</c>, or <c>/</c>).</summary>
    public static readonly SearchValues<byte> TagNameTerminator = SearchValues.Create(" \t\r\n>/"u8);

    /// <summary>ASCII bit that toggles upper-/lowercase on letters.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Returns true when <paramref name="offset"/> sits at a tag-name terminator.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <returns>True when the byte at <paramref name="offset"/> ends the tag name.</returns>
    public static bool IsTagBoundary(ReadOnlySpan<byte> source, int offset) =>
        offset >= source.Length || TagNameTerminator.Contains(source[offset]);

    /// <summary>Tries to match a block-level attr-list-target tag.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Offset just after <c>&lt;</c>.</param>
    /// <param name="nameLen">Matched tag-name byte length on success.</param>
    /// <returns>True on a match.</returns>
    public static bool TryMatchBlockTag(ReadOnlySpan<byte> source, int p, out int nameLen)
    {
        nameLen = 0;
        if (p >= source.Length)
        {
            return false;
        }

        var first = (byte)(source[p] | AsciiCaseBit);
        return DispatchBlock(source, p, first, out nameLen);
    }

    /// <summary>Tries to match a paired-inline tag.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Offset just after <c>&lt;</c>.</param>
    /// <param name="nameLen">Matched tag-name byte length on success.</param>
    /// <returns>True on a match.</returns>
    public static bool TryMatchInlinePairedTag(ReadOnlySpan<byte> source, int p, out int nameLen)
    {
        nameLen = 0;
        if (p >= source.Length)
        {
            return false;
        }

        var first = (byte)(source[p] | AsciiCaseBit);
        return DispatchPairedAtoK(source, p, first, out nameLen)
            || DispatchPairedMtoT(source, p, first, out nameLen);
    }

    /// <summary>Tries to match a void-inline tag.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Offset just after <c>&lt;</c>.</param>
    /// <param name="nameLen">Matched tag-name byte length on success.</param>
    /// <returns>True on a match.</returns>
    public static bool TryMatchInlineVoidTag(ReadOnlySpan<byte> source, int p, out int nameLen)
    {
        nameLen = 0;
        if (p >= source.Length)
        {
            return false;
        }

        var first = (byte)(source[p] | AsciiCaseBit);
        return first switch
        {
            (byte)'i' => TryName(source, p, "img"u8, out nameLen) || TryName(source, p, "input"u8, out nameLen),
            (byte)'b' => TryName(source, p, "br"u8, out nameLen),
            (byte)'h' => TryName(source, p, "hr"u8, out nameLen),
            _ => false,
        };
    }

    /// <summary>Block-tag dispatch by first letter.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="first">Lowered first byte.</param>
    /// <param name="nameLen">Matched length on success.</param>
    /// <returns>True on a match.</returns>
    private static bool DispatchBlock(ReadOnlySpan<byte> source, int p, byte first, out int nameLen) =>
        first switch
        {
            (byte)'h' => TryHeadingTag(source, p, out nameLen),
            (byte)'p' => TryName(source, p, "p"u8, out nameLen),
            (byte)'l' => TryName(source, p, "li"u8, out nameLen),
            (byte)'t' => TryName(source, p, "td"u8, out nameLen) || TryName(source, p, "th"u8, out nameLen),
            (byte)'d' => TryName(source, p, "dd"u8, out nameLen) || TryName(source, p, "dt"u8, out nameLen),
            (byte)'b' => TryName(source, p, "blockquote"u8, out nameLen),
            _ => OutZero(out nameLen),
        };

    /// <summary>Paired-inline dispatch for first letters <c>a</c>..<c>k</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="first">Lowered first byte.</param>
    /// <param name="nameLen">Matched length on success.</param>
    /// <returns>True on a match.</returns>
    private static bool DispatchPairedAtoK(ReadOnlySpan<byte> source, int p, byte first, out int nameLen) =>
        first switch
        {
            (byte)'a' => TryName(source, p, "abbr"u8, out nameLen) || TryName(source, p, "a"u8, out nameLen),
            (byte)'c' => TryName(source, p, "code"u8, out nameLen) || TryName(source, p, "cite"u8, out nameLen),
            (byte)'d' => TryName(source, p, "del"u8, out nameLen),
            (byte)'e' => TryName(source, p, "em"u8, out nameLen),
            (byte)'i' => TryName(source, p, "ins"u8, out nameLen),
            (byte)'k' => TryName(source, p, "kbd"u8, out nameLen),
            _ => OutZero(out nameLen),
        };

    /// <summary>Paired-inline dispatch for first letters <c>m</c>..<c>t</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="first">Lowered first byte.</param>
    /// <param name="nameLen">Matched length on success.</param>
    /// <returns>True on a match.</returns>
    private static bool DispatchPairedMtoT(ReadOnlySpan<byte> source, int p, byte first, out int nameLen) =>
        first switch
        {
            (byte)'m' => TryName(source, p, "mark"u8, out nameLen),
            (byte)'q' => TryName(source, p, "q"u8, out nameLen),
            (byte)'s' => TryInlinePairedS(source, p, out nameLen),
            (byte)'t' => TryName(source, p, "time"u8, out nameLen),
            _ => OutZero(out nameLen),
        };

    /// <summary>Sets <paramref name="nameLen"/> to <c>0</c> and returns false (helper for switch arms).</summary>
    /// <param name="nameLen">Out length.</param>
    /// <returns>Always false.</returns>
    private static bool OutZero(out int nameLen)
    {
        nameLen = 0;
        return false;
    }

    /// <summary>Attempts the <c>s</c>-prefix paired-inline tags.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="nameLen">Matched length on success.</param>
    /// <returns>True on a match.</returns>
    private static bool TryInlinePairedS(ReadOnlySpan<byte> source, int p, out int nameLen) =>
        TryName(source, p, "strong"u8, out nameLen)
        || TryName(source, p, "small"u8, out nameLen)
        || TryName(source, p, "span"u8, out nameLen)
        || TryName(source, p, "sub"u8, out nameLen)
        || TryName(source, p, "sup"u8, out nameLen);

    /// <summary>Attempts an <c>h1</c>..<c>h6</c> match.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="nameLen">Matched length on success.</param>
    /// <returns>True on a match.</returns>
    private static bool TryHeadingTag(ReadOnlySpan<byte> source, int p, out int nameLen)
    {
        const int HeadingNameLen = 2;
        nameLen = 0;
        if (p + 1 >= source.Length || (source[p] | AsciiCaseBit) is not (byte)'h')
        {
            return false;
        }

        var digit = source[p + 1];
        if (digit is < (byte)'1' or > (byte)'6')
        {
            return false;
        }

        if (!IsTagBoundary(source, p + HeadingNameLen))
        {
            return false;
        }

        nameLen = HeadingNameLen;
        return true;
    }

    /// <summary>Tries to match a literal tag name at <paramref name="p"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="name">Lowercase ASCII tag name.</param>
    /// <param name="nameLen">Matched length on success.</param>
    /// <returns>True on a match.</returns>
    private static bool TryName(ReadOnlySpan<byte> source, int p, ReadOnlySpan<byte> name, out int nameLen)
    {
        nameLen = 0;
        if (p + name.Length > source.Length)
        {
            return false;
        }

        for (var i = 0; i < name.Length; i++)
        {
            if ((source[p + i] | AsciiCaseBit) != name[i])
            {
                return false;
            }
        }

        if (!IsTagBoundary(source, p + name.Length))
        {
            return false;
        }

        nameLen = name.Length;
        return true;
    }
}
