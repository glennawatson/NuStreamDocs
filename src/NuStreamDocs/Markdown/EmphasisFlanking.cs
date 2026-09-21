// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Markdown;

/// <summary>Decides whether an emphasis marker run can open or close emphasis from the characters around it.</summary>
internal static class EmphasisFlanking
{
    /// <summary>First byte of a multi-byte UTF-8 sequence.</summary>
    private const byte FirstNonAsciiByte = 0x80;

    /// <summary>Form-feed byte, whitespace in CommonMark.</summary>
    private const byte FormFeed = 0x0C;

    /// <summary>Underscore byte.</summary>
    private const byte Underscore = (byte)'_';

    /// <summary>Kind of the character next to a marker run.</summary>
    private enum CharKind
    {
        /// <summary>Whitespace, or the edge of the text.</summary>
        Whitespace = 0,

        /// <summary>Punctuation or a symbol.</summary>
        Punctuation = 1,

        /// <summary>Any other character.</summary>
        Other = 2,
    }

    /// <summary>Classifies a marker run.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="start">Index of the run's first byte.</param>
    /// <param name="length">Length of the run.</param>
    /// <param name="marker">Marker byte.</param>
    /// <returns>Whether the run can open emphasis and whether it can close emphasis.</returns>
    internal static (bool CanOpen, bool CanClose) Classify(ReadOnlySpan<byte> source, int start, int length, byte marker)
    {
        var before = KindBefore(source, start);
        var after = KindAt(source, start + length);
        var leftFlanking = after is not CharKind.Whitespace && (after is not CharKind.Punctuation || before is not CharKind.Other);
        var rightFlanking = before is not CharKind.Whitespace && (before is not CharKind.Punctuation || after is not CharKind.Other);
        return marker is Underscore
            ? (leftFlanking && (!rightFlanking || before is CharKind.Punctuation), rightFlanking && (!leftFlanking || after is CharKind.Punctuation))
            : (leftFlanking, rightFlanking);
    }

    /// <summary>Classifies the character that starts at an index.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Index of the character's first byte.</param>
    /// <returns>The character kind; whitespace at the end of the text.</returns>
    private static CharKind KindAt(ReadOnlySpan<byte> source, int index)
    {
        if (index >= source.Length)
        {
            return CharKind.Whitespace;
        }

        var b = source[index];
        if (b < FirstNonAsciiByte)
        {
            return KindOfAscii(b);
        }

        _ = Rune.DecodeFromUtf8(source[index..], out var rune, out _);
        return KindOfRune(rune);
    }

    /// <summary>Classifies the character that ends just before an index.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Index one past the character's last byte.</param>
    /// <returns>The character kind; whitespace at the start of the text.</returns>
    private static CharKind KindBefore(ReadOnlySpan<byte> source, int index)
    {
        if (index is 0)
        {
            return CharKind.Whitespace;
        }

        var b = source[index - 1];
        if (b < FirstNonAsciiByte)
        {
            return KindOfAscii(b);
        }

        _ = Rune.DecodeLastFromUtf8(source[..index], out var rune, out _);
        return KindOfRune(rune);
    }

    /// <summary>Classifies an ASCII byte.</summary>
    /// <param name="b">The byte.</param>
    /// <returns>The character kind.</returns>
    private static CharKind KindOfAscii(byte b)
    {
        if (b is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or FormFeed)
        {
            return CharKind.Whitespace;
        }

        return InlineEscape.IsAsciiPunct(b) ? CharKind.Punctuation : CharKind.Other;
    }

    /// <summary>Classifies a non-ASCII character.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The character kind.</returns>
    private static CharKind KindOfRune(Rune rune)
    {
        if (Rune.IsWhiteSpace(rune))
        {
            return CharKind.Whitespace;
        }

        return Rune.IsPunctuation(rune) || Rune.IsSymbol(rune) ? CharKind.Punctuation : CharKind.Other;
    }
}
