// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using NuStreamDocs.Common;

namespace NuStreamDocs.Toc;

/// <summary>
/// Pure slug helper that maps heading text to ASCII identifier-safe
/// byte sequences and resolves duplicates within a single page.
/// </summary>
/// <remarks>
/// Algorithm matches the mkdocs <c>toc</c> default slug:
/// <list type="bullet">
/// <item><description>Lowercase ASCII letters, digits, and hyphens are retained.</description></item>
/// <item><description>Whitespace and punctuation collapse to a single hyphen.</description></item>
/// <item><description>Leading and trailing hyphens are trimmed.</description></item>
/// <item><description>Duplicates within the same page receive a numeric <c>-N</c> suffix starting at <c>2</c>.</description></item>
/// </list>
/// Slug bytes are always ASCII per this rule, so the rewriter and TOC
/// fragment renderer can splice them straight into the output stream.
/// </remarks>
internal static class HeadingSlugifier
{
    /// <summary>The hyphen byte used as the only allowed punctuation in slugs.</summary>
    private const byte HyphenByte = (byte)'-';

    /// <summary>ASCII offset to convert an upper-case letter to its lower-case counterpart.</summary>
    private const int AsciiUpperToLowerOffset = 32;

    /// <summary>Stack-buffer size used by <see cref="SlugifyToBytes"/> before falling back to the heap.</summary>
    private const int StackSlugBufferSize = 256;

    /// <summary>Byte form of the slug used when a heading reduces to nothing.</summary>
    private static readonly byte[] FallbackSlugBytes = [.. "section"u8];

    /// <summary>Assigns slugs to <paramref name="headings"/>, deduplicating within the page.</summary>
    /// <param name="html">Original HTML snapshot, used to read each heading's existing-id span and inner text bytes.</param>
    /// <param name="headings">Heading records to populate; updated in place via a returned new array.</param>
    /// <returns>A tuple of <c>(headings with slug populated, collisionCount)</c>.</returns>
    public static (Heading[] Slugged, int Collisions) AssignSlugs(ReadOnlySpan<byte> html, Heading[] headings)
    {
        ArgumentNullException.ThrowIfNull(headings);
        if (headings.Length is 0)
        {
            return ([], 0);
        }

        var result = new Heading[headings.Length];
        var seen = new Dictionary<byte[], int>(headings.Length, ByteArrayComparer.Instance);
        var collisions = 0;

        // One reusable text-decode buffer for the whole page; reset between headings.
        using var textRental = PageBuilderPool.Rent(64);
        var textBuffer = textRental.Writer;

        for (var i = 0; i < headings.Length; i++)
        {
            var h = headings[i];
            byte[] baseSlug;
            if (h.HasExistingId)
            {
                baseSlug = h.ExistingIdBytes(html).ToArray();
            }
            else
            {
                textBuffer.ResetWrittenCount();
                HeadingScanner.DecodeTextInto(html, in h, textBuffer);
                baseSlug = SlugifyToBytes(textBuffer.WrittenSpan);
            }

            var finalSlug = baseSlug;
            if (seen.TryGetValue(baseSlug, out var hit))
            {
                collisions++;
                var next = hit + 1;
                finalSlug = AppendSuffix(baseSlug, next);
                seen[baseSlug] = next;
            }
            else
            {
                seen[baseSlug] = 1;
            }

            result[i] = h with { Slug = finalSlug };
        }

        return (result, collisions);
    }

    /// <summary>Reduces <paramref name="text"/> bytes to a slug byte array.</summary>
    /// <param name="text">Raw heading text bytes (UTF-8). May contain leading/trailing whitespace and inline punctuation.</param>
    /// <returns>ASCII slug bytes; never empty (returns the fallback when input strips to nothing).</returns>
    public static byte[] SlugifyToBytes(ReadOnlySpan<byte> text)
    {
        if (text.IsEmpty)
        {
            return FallbackSlugBytes;
        }

        // Output is bounded by input — each byte either emits one byte
        // or a hyphen, but the hyphen is only flushed after at least one
        // slug byte was already written, so length never exceeds input.
        var maxLen = text.Length;
        if (maxLen <= StackSlugBufferSize)
        {
            Span<byte> tmp = stackalloc byte[StackSlugBufferSize];
            var len = WriteSlugCore(text, tmp);
            return len is 0 ? FallbackSlugBytes : tmp[..len].ToArray();
        }

        var rented = ArrayPool<byte>.Shared.Rent(maxLen);
        try
        {
            var len = WriteSlugCore(text, rented);
            return len is 0 ? FallbackSlugBytes : rented.AsSpan(0, len).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Writes the slug derived from <paramref name="text"/> into <paramref name="dst"/>; returns the byte count written.</summary>
    /// <param name="text">Source UTF-8 text.</param>
    /// <param name="dst">Destination buffer; must be at least <c>text.Length</c> long.</param>
    /// <returns>Bytes written (0 when the slug is empty).</returns>
    private static int WriteSlugCore(ReadOnlySpan<byte> text, in Span<byte> dst)
    {
        var len = 0;
        var pendingHyphen = false;
        for (var i = 0; i < text.Length; i++)
        {
            var slugByte = ToSlugByte(text[i]);
            if (slugByte is 0)
            {
                pendingHyphen = len > 0;
                continue;
            }

            if (pendingHyphen)
            {
                dst[len++] = HyphenByte;
            }

            dst[len++] = slugByte;
            pendingHyphen = false;
        }

        return len;
    }

    /// <summary>Folds a single UTF-8 byte to its slug-rule byte (lowercase letter, digit) or <c>0</c> when not slug-eligible.</summary>
    /// <param name="b">Source byte.</param>
    /// <returns>The slug byte, or <c>0</c> for non-slug input.</returns>
    private static byte ToSlugByte(byte b) => b switch
    {
        >= (byte)'A' and <= (byte)'Z' => (byte)(b + AsciiUpperToLowerOffset),
        >= (byte)'a' and <= (byte)'z' => b,
        >= (byte)'0' and <= (byte)'9' => b,
        _ => 0,
    };

    /// <summary>Allocates a fresh <c>{baseSlug}-{n}</c> byte array.</summary>
    /// <param name="baseSlug">The base slug bytes.</param>
    /// <param name="n">Suffix integer (1-based, but always &gt;= 2 in practice).</param>
    /// <returns>The combined slug bytes.</returns>
    private static byte[] AppendSuffix(byte[] baseSlug, int n)
    {
        Span<byte> digits = stackalloc byte[16];
        if (!n.TryFormat(digits, out var digitCount, default, CultureInfo.InvariantCulture))
        {
            return baseSlug;
        }

        var combined = new byte[baseSlug.Length + 1 + digitCount];
        baseSlug.CopyTo(combined, 0);
        combined[baseSlug.Length] = HyphenByte;
        digits[..digitCount].CopyTo(combined.AsSpan(baseSlug.Length + 1));
        return combined;
    }
}
