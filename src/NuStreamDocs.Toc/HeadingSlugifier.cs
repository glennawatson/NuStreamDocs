// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Toc;

/// <summary>
/// Pure slug helper that maps heading text to ASCII identifier-safe
/// strings and resolves duplicates within a single page.
/// </summary>
/// <remarks>
/// Algorithm matches the mkdocs <c>toc</c> default slug:
/// <list type="bullet">
/// <item><description>Lowercase ASCII letters, digits, and hyphens are retained.</description></item>
/// <item><description>Whitespace and punctuation collapse to a single hyphen.</description></item>
/// <item><description>Leading and trailing hyphens are trimmed.</description></item>
/// <item><description>Duplicates within the same page receive a numeric <c>-N</c> suffix starting at <c>2</c>.</description></item>
/// </list>
/// </remarks>
internal static class HeadingSlugifier
{
    /// <summary>The hyphen byte/character used as the only allowed punctuation in slugs.</summary>
    private const char Hyphen = '-';

    /// <summary>Slug used when a heading reduces to nothing after stripping.</summary>
    private const string FallbackSlug = "section";

    /// <summary>ASCII offset to convert an upper-case letter to its lower-case counterpart.</summary>
    private const int AsciiUpperToLowerOffset = 32;

    /// <summary>Assigns slugs to <paramref name="headings"/>, deduplicating within the page.</summary>
    /// <param name="html">Original HTML snapshot, used to decode each heading's inner text.</param>
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
        var seen = new Dictionary<string, int>(headings.Length, StringComparer.Ordinal);
        var collisions = 0;
        for (var i = 0; i < headings.Length; i++)
        {
            var h = headings[i];
            string baseSlug;
            if (h.ExistingId is { Length: > 0 })
            {
                baseSlug = h.ExistingId;
            }
            else
            {
                var text = HeadingScanner.DecodeText(html, in h);
                baseSlug = Slugify(text);
            }

            var finalSlug = baseSlug;
            if (seen.TryGetValue(baseSlug, out var hit))
            {
                collisions++;
                var next = hit + 1;
                finalSlug = $"{baseSlug}-{next.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
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

    /// <summary>Reduces <paramref name="text"/> to a slug.</summary>
    /// <param name="text">Raw heading text.</param>
    /// <returns>ASCII slug; never empty.</returns>
    public static string Slugify(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return FallbackSlug;
        }

        var sb = new StringBuilder(text.Length);
        var pendingHyphen = false;
        for (var i = 0; i < text.Length; i++)
        {
            pendingHyphen = AppendChar(sb, text[i], pendingHyphen);
        }

        return sb.Length is 0 ? FallbackSlug : sb.ToString();
    }

    /// <summary>Appends a single character of slug input to <paramref name="sb"/>.</summary>
    /// <param name="sb">Output buffer.</param>
    /// <param name="c">Character being inspected.</param>
    /// <param name="pendingHyphen">True if a hyphen is queued from prior non-slug characters.</param>
    /// <returns>The new pending-hyphen state.</returns>
    private static bool AppendChar(StringBuilder sb, char c, bool pendingHyphen)
    {
        if (c is >= 'A' and <= 'Z')
        {
            FlushPendingHyphen(sb, pendingHyphen);
            sb.Append((char)(c + AsciiUpperToLowerOffset));
            return false;
        }

        if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
        {
            FlushPendingHyphen(sb, pendingHyphen);
            sb.Append(c);
            return false;
        }

        // Anything else collapses to a hyphen, but we coalesce runs.
        return sb.Length is not 0 || pendingHyphen;
    }

    /// <summary>Writes a queued hyphen to <paramref name="sb"/> when one is pending and the buffer is non-empty.</summary>
    /// <param name="sb">Output buffer.</param>
    /// <param name="pendingHyphen">Whether a hyphen is queued.</param>
    private static void FlushPendingHyphen(StringBuilder sb, bool pendingHyphen)
    {
        if (!pendingHyphen || sb.Length is 0)
        {
            return;
        }

        sb.Append(Hyphen);
    }
}
