// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>Resolves a face against the Google Fonts <c>css2</c> API and downloads the referenced woff2 files (filtering to the requested script subsets, since css2 returns them all).</summary>
public sealed class GoogleFontProvider : IFontProvider
{
    /// <summary>Shared instance.</summary>
    public static readonly GoogleFontProvider Instance = new();

    /// <summary>UTF-8 subset name meaning "every subset the provider offers".</summary>
    private static readonly byte[] AllSubsets = [.. "all"u8];

    /// <summary>Initializes a new instance of the <see cref="GoogleFontProvider"/> class.</summary>
    private GoogleFontProvider()
    {
    }

    /// <inheritdoc/>
    public async ValueTask<FontResource[]> ResolveAsync(
        FontFace face,
        byte[][] requestedSubsets,
        FontDownloadCache cache,
        DirectoryPath inputRoot,
        bool[]? subsetUsage,
        CancellationToken cancellationToken)
    {
        _ = inputRoot;
        ArgumentNullException.ThrowIfNull(cache);

        var keepAll = requestedSubsets is [] ||
                      (requestedSubsets is [var only] && only.AsSpan().SequenceEqual(AllSubsets));
        var cssBytes = await cache.GetAsync(BuildStylesheetUrl(face), cancellationToken).ConfigureAwait(false);
        var parsed = Css2StylesheetParser.Parse(cssBytes);
        List<FontResource> resources = [];
        for (var i = 0; i < parsed.Length; i++)
        {
            var entry = parsed[i];
            if (!keepAll && !SubsetWanted(entry.SubsetName, requestedSubsets))
            {
                continue;
            }

            if (subsetUsage is not null && entry.UnicodeRange is [_, ..] &&
                !UnicodeRangeMatcher.Overlaps(entry.UnicodeRange, subsetUsage))
            {
                // auto mode: this subset covers nothing the site uses — don't even download it.
                continue;
            }

            var woff2 = await cache.GetAsync(entry.Woff2Url, cancellationToken).ConfigureAwait(false);
            resources.Add(
                new(
                    (byte[])face.FamilyBytes.Clone(),
                    entry.Weight,
                    entry.Style,
                    entry.UnicodeRange,
                    woff2,
                    entry.Woff2Url));
        }

        return [.. resources];
    }

    /// <summary>Builds the <c>css2</c> stylesheet URL for <paramref name="face"/> (all subsets — css2 ignores a <c>subset=</c> param, so filtering happens after parsing).</summary>
    /// <param name="face">The declared face.</param>
    /// <returns>The stylesheet URL.</returns>
    internal static ApiCompatString BuildStylesheetUrl(in FontFace face)
    {
        var sb = new StringBuilder("https://fonts.googleapis.com/css2?family=");
        sb.Append(Encoding.UTF8.GetString(face.FamilyBytes).Replace(' ', '+'));

        var hasItalic = false;
        for (var i = 0; i < face.Styles.Length; i++)
        {
            hasItalic |= face.Styles[i] == FontStyle.Italic;
        }

        var weights = face.Weights;
        Array.Sort(weights);
        sb.Append(hasItalic ? ":ital,wght@" : ":wght@");
        var first = true;
        for (var ital = 0; ital <= (hasItalic ? 1 : 0); ital++)
        {
            for (var w = 0; w < weights.Length; w++)
            {
                if (!first)
                {
                    sb.Append(';');
                }

                first = false;
                if (hasItalic)
                {
                    sb.Append(ital).Append(',');
                }

                sb.Append(weights[w]);
            }
        }

        sb.Append("&display=").Append(DisplayToken(face.Display));
        return sb.ToString();
    }

    /// <summary>Returns whether a parsed block's subset name is one the caller asked for; an unlabeled block is always kept.</summary>
    /// <param name="subsetName">The block's subset name (empty when the stylesheet didn't label it).</param>
    /// <param name="requested">Requested subset names.</param>
    /// <returns><see langword="true"/> when the block should be kept.</returns>
    private static bool SubsetWanted(byte[] subsetName, byte[][] requested)
    {
        if (subsetName is [])
        {
            return true;
        }

        for (var i = 0; i < requested.Length; i++)
        {
            if (subsetName.AsSpan().SequenceEqual(requested[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Maps a <see cref="FontDisplay"/> to its CSS keyword.</summary>
    /// <param name="display">The display mode.</param>
    /// <returns>The keyword string.</returns>
    private static string DisplayToken(FontDisplay display) => display switch
    {
        FontDisplay.Block => "block",
        FontDisplay.Swap => "swap",
        FontDisplay.Fallback => "fallback",
        FontDisplay.Optional => "optional",
        _ => "auto"
    };
}
