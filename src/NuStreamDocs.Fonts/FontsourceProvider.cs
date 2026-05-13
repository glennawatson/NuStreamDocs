// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>Resolves a face against the Fontsource catalogue served from jsDelivr and downloads the referenced woff2 files.</summary>
public sealed class FontsourceProvider : IFontProvider
{
    /// <summary>Shared instance.</summary>
    public static readonly FontsourceProvider Instance = new();

    /// <summary>Default subset name used when none is requested.</summary>
    private static readonly byte[] LatinSubset = [.. "latin"u8];

    /// <summary>Initializes a new instance of the <see cref="FontsourceProvider"/> class.</summary>
    private FontsourceProvider()
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
        _ = subsetUsage;
        ArgumentNullException.ThrowIfNull(cache);

        var subsets = requestedSubsets is [_, ..] ? requestedSubsets : [LatinSubset];
        var id = FamilyId(face.FamilyBytes);
        List<FontResource> resources = [];
        for (var s = 0; s < subsets.Length; s++)
        {
            var subset = Encoding.UTF8.GetString(subsets[s]);
            for (var w = 0; w < face.Weights.Length; w++)
            {
                for (var st = 0; st < face.Styles.Length; st++)
                {
                    var cssUrl = BuildStylesheetUrl(id, subset, face.Weights[w], face.Styles[st]);
                    await AddFromStylesheetAsync(face, cssUrl, cache, resources, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        return [.. resources];
    }

    /// <summary>Builds the jsDelivr stylesheet URL for one weight/style/subset of a Fontsource family.</summary>
    /// <param name="id">Fontsource package id (lowercase, hyphenated).</param>
    /// <param name="subset">Subset name.</param>
    /// <param name="weight">Numeric weight.</param>
    /// <param name="style">Upright or italic.</param>
    /// <returns>The stylesheet URL.</returns>
    internal static ApiCompatString BuildStylesheetUrl(string id, string subset, int weight, FontStyle style)
    {
        var sb = new StringBuilder("https://cdn.jsdelivr.net/npm/@fontsource/");
        sb.Append(id).Append("@latest/").Append(subset).Append('-').Append(weight);
        if (style == FontStyle.Italic)
        {
            sb.Append("-italic");
        }

        sb.Append(".css");
        return sb.ToString();
    }

    /// <summary>Lowercases ASCII and replaces spaces with hyphens to form a Fontsource package id.</summary>
    /// <param name="familyBytes">UTF-8 family name.</param>
    /// <returns>The package id string.</returns>
    private static string FamilyId(byte[] familyBytes)
    {
        var chars = new char[familyBytes.Length];
        for (var i = 0; i < familyBytes.Length; i++)
        {
            chars[i] = familyBytes[i] == (byte)' ' ? '-' : (char)AsciiByteHelpers.ToAsciiLowerByte(familyBytes[i]);
        }

        return new(chars);
    }

    /// <summary>Fetches one stylesheet, resolves its (relative) woff2 URL against the stylesheet URL, downloads it, and appends a resource.</summary>
    /// <param name="face">The declared face (its family name is reused).</param>
    /// <param name="stylesheetUrl">The stylesheet URL.</param>
    /// <param name="cache">Download cache.</param>
    /// <param name="resources">Destination list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the woff2 file has been resolved.</returns>
    private static async Task AddFromStylesheetAsync(
        FontFace face,
        ApiCompatString stylesheetUrl,
        FontDownloadCache cache,
        List<FontResource> resources,
        CancellationToken cancellationToken)
    {
        var cssBytes = await cache.GetAsync(stylesheetUrl, cancellationToken).ConfigureAwait(false);
        var faces = Css2StylesheetParser.Parse(cssBytes);
        var baseUri = new Uri(stylesheetUrl.Value ?? string.Empty, UriKind.Absolute);
        for (var i = 0; i < faces.Length; i++)
        {
            var absolute = new Uri(baseUri, faces[i].Woff2Url.Value ?? string.Empty).ToString();
            var woff2 = await cache.GetAsync(absolute, cancellationToken).ConfigureAwait(false);
            resources.Add(
                new(
                    (byte[])face.FamilyBytes.Clone(),
                    faces[i].Weight,
                    faces[i].Style,
                    faces[i].UnicodeRange,
                    woff2,
                    absolute));
        }
    }
}
