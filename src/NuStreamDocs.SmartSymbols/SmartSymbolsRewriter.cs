// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.SmartSymbols;

/// <summary>
/// Stateless UTF-8 smart-symbols rewriter. Replaces the pymdownx
/// defaults inline; fenced-code regions and inline-code spans are
/// copied through verbatim.
/// </summary>
internal static class SmartSymbolsRewriter
{
    /// <summary>Byte length of three-byte tokens such as <c>(c)</c>, <c>+/-</c>, <c>--&gt;</c>, <c>1/2</c>.</summary>
    private const int TripleTokenLength = 3;

    /// <summary>Byte length of four-byte tokens such as <c>(tm)</c>, <c>&lt;--&gt;</c>, <c>&lt;==&gt;</c>.</summary>
    private const int QuadTokenLength = 4;

    /// <summary>Offset of the second character in a two-byte suffix (e.g. the second <c>/</c> in <c>+/-</c>).</summary>
    private const int SuffixSecondByteOffset = 2;

    /// <summary>Offset of the third character in a three-byte suffix (e.g. the trailing <c>&gt;</c> in <c>&lt;--&gt;</c>).</summary>
    private const int SuffixThirdByteOffset = 3;

    /// <summary>Gets the UTF-8 encoding of <c>©</c>.</summary>
    private static ReadOnlySpan<byte> Copyright => "©"u8;

    /// <summary>Gets the UTF-8 encoding of <c>®</c>.</summary>
    private static ReadOnlySpan<byte> Registered => "®"u8;

    /// <summary>Gets the UTF-8 encoding of <c>™</c>.</summary>
    private static ReadOnlySpan<byte> Trademark => "™"u8;

    /// <summary>Gets the UTF-8 encoding of <c>℅</c>.</summary>
    private static ReadOnlySpan<byte> CareOf => "℅"u8;

    /// <summary>Gets the UTF-8 encoding of <c>±</c>.</summary>
    private static ReadOnlySpan<byte> PlusMinus => "±"u8;

    /// <summary>Gets the UTF-8 encoding of <c>≠</c>.</summary>
    private static ReadOnlySpan<byte> NotEqual => "≠"u8;

    /// <summary>Gets the UTF-8 encoding of <c>→</c> (single arrow right).</summary>
    private static ReadOnlySpan<byte> ArrowRight => "→"u8;

    /// <summary>Gets the UTF-8 encoding of <c>←</c> (single arrow left).</summary>
    private static ReadOnlySpan<byte> ArrowLeft => "←"u8;

    /// <summary>Gets the UTF-8 encoding of <c>↔</c> (double-headed arrow).</summary>
    private static ReadOnlySpan<byte> ArrowBoth => "↔"u8;

    /// <summary>Gets the UTF-8 encoding of <c>⇒</c> (double-stroke arrow right).</summary>
    private static ReadOnlySpan<byte> DoubleArrowRight => "⇒"u8;

    /// <summary>Gets the UTF-8 encoding of <c>⇐</c> (double-stroke arrow left).</summary>
    private static ReadOnlySpan<byte> DoubleArrowLeft => "⇐"u8;

    /// <summary>Gets the UTF-8 encoding of <c>⇔</c> (double-stroke double-headed arrow).</summary>
    private static ReadOnlySpan<byte> DoubleArrowBoth => "⇔"u8;

    /// <summary>Gets the UTF-8 encoding of <c>¼</c>.</summary>
    private static ReadOnlySpan<byte> OneQuarter => "¼"u8;

    /// <summary>Gets the UTF-8 encoding of <c>½</c>.</summary>
    private static ReadOnlySpan<byte> OneHalf => "½"u8;

    /// <summary>Gets the UTF-8 encoding of <c>¾</c>.</summary>
    private static ReadOnlySpan<byte> ThreeQuarters => "¾"u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        CodeAwareRewriter.Run(source, writer, TrySubstitute);

    /// <summary>Tries every smart-symbol pattern at <paramref name="offset"/>; emits the replacement and reports the consumed byte count on success.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Number of input bytes the match covered.</param>
    /// <returns>True when a substitution fired.</returns>
    private static bool TrySubstitute(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        var first = NormaliseFirstByte(source[offset]);
        return first switch
        {
            (byte)'(' => TryParenSymbol(source, offset, writer, out consumed),
            (byte)'+' or (byte)'=' => TryPlusOrEquals(source, offset, writer, out consumed),
            (byte)'-' or (byte)'<' => TryDashOrLt(source, offset, writer, out consumed),
            (byte)'c' => TryCareOf(source, offset, writer, out consumed),
            (byte)'1' => TryNumericFraction(source, offset, writer, out consumed),
            _ => false,
        };
    }

    /// <summary>Folds case-variants and the <c>3</c>/<c>1</c> fraction-leading digits to a single dispatch byte.</summary>
    /// <param name="b">First byte of the candidate token.</param>
    /// <returns>Canonical dispatch byte.</returns>
    private static byte NormaliseFirstByte(byte b) => b switch
    {
        (byte)'C' => (byte)'c',
        (byte)'3' => (byte)'1',
        _ => b,
    };

    /// <summary>Dispatches the <c>+/-</c> and <c>=/=</c> / <c>==&gt;</c> tokens.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryPlusOrEquals(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed) =>
        source[offset] is (byte)'+'
            ? TryPlusMinus(source, offset, writer, out consumed)
            : TryEqualsForm(source, offset, writer, out consumed);

    /// <summary>Dispatches the dash- and lt-led arrows (<c>--&gt;</c>, <c>&lt;--</c>, <c>&lt;--&gt;</c>, etc.).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryDashOrLt(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed) =>
        source[offset] is (byte)'-'
            ? TryArrowFromDash(source, offset, writer, out consumed)
            : TryArrowFromLt(source, offset, writer, out consumed);

    /// <summary>Dispatches the leading-digit fractions (<c>1/2</c>, <c>1/4</c>, <c>3/4</c>) to their handlers.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset on the leading digit.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryNumericFraction(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed) =>
        source[offset] is (byte)'1'
            ? TryFractionStartingOne(source, offset, writer, out consumed)
            : TryThreeQuarters(source, offset, writer, out consumed);

    /// <summary><c>(c)</c>, <c>(r)</c>, <c>(tm)</c> — case-insensitive, no word-boundary check.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryParenSymbol(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        var letter = TryParenLetter(source, offset);
        if (!letter.IsEmpty)
        {
            writer.Write(letter);
            consumed = TripleTokenLength;
            return true;
        }

        if (!IsTrademark(source, offset))
        {
            return false;
        }

        writer.Write(Trademark);
        consumed = QuadTokenLength;
        return true;
    }

    /// <summary>Returns the matching glyph for <c>(c)</c> / <c>(r)</c> at <paramref name="offset"/>; an empty span when no match.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor at the opening <c>(</c>.</param>
    /// <returns>The replacement bytes, or empty.</returns>
    private static ReadOnlySpan<byte> TryParenLetter(ReadOnlySpan<byte> source, int offset)
    {
        if (offset + SuffixSecondByteOffset >= source.Length
            || source[offset + SuffixSecondByteOffset] is not (byte)')')
        {
            return default;
        }

        return source[offset + 1] switch
        {
            (byte)'c' or (byte)'C' => Copyright,
            (byte)'r' or (byte)'R' => Registered,
            _ => default,
        };
    }

    /// <summary>Returns true when the bytes at <paramref name="offset"/> spell <c>(tm)</c> case-insensitively.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor at the opening <c>(</c>.</param>
    /// <returns>True when the four-byte trademark token matches.</returns>
    private static bool IsTrademark(ReadOnlySpan<byte> source, int offset) =>
        offset + SuffixThirdByteOffset < source.Length
        && source[offset + 1] is (byte)'t' or (byte)'T'
        && source[offset + SuffixSecondByteOffset] is (byte)'m' or (byte)'M'
        && source[offset + SuffixThirdByteOffset] is (byte)')';

    /// <summary><c>+/-</c> → <c>±</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryPlusMinus(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (offset + SuffixSecondByteOffset >= source.Length
            || source[offset + 1] is not (byte)'/'
            || source[offset + SuffixSecondByteOffset] is not (byte)'-')
        {
            return false;
        }

        writer.Write(PlusMinus);
        consumed = TripleTokenLength;
        return true;
    }

    /// <summary><c>=/=</c> → <c>≠</c>, <c>==&gt;</c> → <c>⇒</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryEqualsForm(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (offset + SuffixSecondByteOffset >= source.Length)
        {
            return false;
        }

        var glyph = (source[offset + 1], source[offset + SuffixSecondByteOffset]) switch
        {
            ((byte)'/', (byte)'=') => NotEqual,
            ((byte)'=', (byte)'>') => DoubleArrowRight,
            _ => default,
        };

        if (glyph.IsEmpty)
        {
            return false;
        }

        writer.Write(glyph);
        consumed = TripleTokenLength;
        return true;
    }

    /// <summary><c>--&gt;</c> → <c>→</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryArrowFromDash(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (offset + SuffixSecondByteOffset >= source.Length
            || source[offset + 1] is not (byte)'-'
            || source[offset + SuffixSecondByteOffset] is not (byte)'>')
        {
            return false;
        }

        writer.Write(ArrowRight);
        consumed = TripleTokenLength;
        return true;
    }

    /// <summary><c>&lt;--&gt;</c> / <c>&lt;==&gt;</c> / <c>&lt;--</c> / <c>&lt;==</c>. Tries longest forms first.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryArrowFromLt(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (offset + SuffixSecondByteOffset >= source.Length)
        {
            return false;
        }

        var stroke = source[offset + 1];
        if (stroke is not (byte)'-' and not (byte)'=' || source[offset + SuffixSecondByteOffset] != stroke)
        {
            return false;
        }

        var bidirectional = offset + SuffixThirdByteOffset < source.Length
            && source[offset + SuffixThirdByteOffset] is (byte)'>';

        writer.Write(ArrowForLt(stroke, bidirectional));
        consumed = bidirectional ? QuadTokenLength : TripleTokenLength;
        return true;
    }

    /// <summary>Picks the arrow glyph for <c>&lt;--</c> / <c>&lt;==</c> with optional trailing <c>&gt;</c>.</summary>
    /// <param name="stroke">Either <c>-</c> or <c>=</c>.</param>
    /// <param name="bidirectional">True when the suffix is <c>&gt;</c>.</param>
    /// <returns>UTF-8 bytes of the chosen glyph.</returns>
    private static ReadOnlySpan<byte> ArrowForLt(byte stroke, bool bidirectional) =>
        (stroke, bidirectional) switch
        {
            ((byte)'-', true) => ArrowBoth,
            ((byte)'-', false) => ArrowLeft,
            ((byte)'=', true) => DoubleArrowBoth,
            _ => DoubleArrowLeft,
        };

    /// <summary><c>c/o</c> → <c>℅</c>; word-boundary on both sides.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryCareOf(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (!AsciiWordBoundary.TryMatchBoundedIgnoreAsciiCase(source, offset, "c/o"u8))
        {
            return false;
        }

        writer.Write(CareOf);
        consumed = TripleTokenLength;
        return true;
    }

    /// <summary><c>1/2</c> or <c>1/4</c> → <c>½</c> / <c>¼</c>; word-boundary on both sides.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryFractionStartingOne(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        var glyph = source[offset..] switch
        {
            _ when AsciiWordBoundary.TryMatchBounded(source, offset, "1/2"u8) => OneHalf,
            _ when AsciiWordBoundary.TryMatchBounded(source, offset, "1/4"u8) => OneQuarter,
            _ => default,
        };

        if (glyph.IsEmpty)
        {
            return false;
        }

        writer.Write(glyph);
        consumed = TripleTokenLength;
        return true;
    }

    /// <summary><c>3/4</c> → <c>¾</c>; word-boundary on both sides.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryThreeQuarters(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (!AsciiWordBoundary.TryMatchBounded(source, offset, "3/4"u8))
        {
            return false;
        }

        writer.Write(ThreeQuarters);
        consumed = TripleTokenLength;
        return true;
    }
}
