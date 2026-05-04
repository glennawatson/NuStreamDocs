// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using NuStreamDocs.Common;
using NuStreamDocs.Templating;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material;

/// <summary>
/// Material-styled theme: pre-compiled page + partial templates plus
/// the shipped CSS / JS bundle.
/// </summary>
/// <remarks>
/// Constructed once per build and shared across worker threads. The
/// <see cref="Page"/> template is the entry point; partials resolve
/// through <see cref="Partials"/> when a worker calls
/// <c>page.Render(data, theme.Partials, writer)</c>. Static asset
/// bytes are exposed through <see cref="StaticAssets"/> so the
/// pipeline can write them out alongside rendered pages.
/// </remarks>
public sealed class MaterialTheme : IThemePackage
{
    /// <summary>Embedded asset paths of the theme's static files.</summary>
    private static readonly FilePath[] StaticAssetPaths =
    [
        "assets/stylesheets/material.min.css",
        "assets/stylesheets/palette.min.css",
        "assets/javascripts/material.min.js",
    ];

    /// <summary>Static assets as an indexable snapshot for write-out loops.</summary>
    private readonly (FilePath RelativePath, byte[] Bytes)[] _staticAssetEntries;

    /// <summary>Initializes a new instance of the <see cref="MaterialTheme"/> class.</summary>
    /// <param name="page">Compiled top-level page template.</param>
    /// <param name="partials">Compiled partial templates.</param>
    /// <param name="staticAssets">Static-asset bundle keyed by relative path.</param>
    private MaterialTheme(Template page, Dictionary<byte[], Template> partials, Dictionary<FilePath, byte[]> staticAssets)
    {
        Page = page;
        Partials = partials;
        StaticAssets = new(staticAssets);
        _staticAssetEntries = ThemeModelLoader.BuildStaticAssetEntries(staticAssets);
    }

    /// <summary>Gets the compiled top-level page template.</summary>
    public Template Page { get; }

    /// <summary>Gets the compiled partial registry, keyed by UTF-8 partial-name bytes.</summary>
    /// <remarks>Built once at theme load and probed repeatedly across page renders via the byte-keyed alternate-lookup pattern.</remarks>
    public Dictionary<byte[], Template> Partials { get; }

    /// <summary>
    /// Gets the static-asset map, keyed by relative output path. Plain
    /// <see cref="Dictionary{TKey, TValue}"/> exposed read-only — the bundle is tiny
    /// (3-5 entries) and the freeze cost wouldn't repay itself.
    /// </summary>
    public ReadOnlyDictionary<FilePath, byte[]> StaticAssets { get; }

    /// <inheritdoc/>
    (FilePath RelativePath, byte[] Bytes)[] IThemePackage.StaticAssetEntries => _staticAssetEntries;

    /// <summary>Loads and compiles the Material theme from its embedded resources.</summary>
    /// <returns>A ready-to-use <see cref="MaterialTheme"/>.</returns>
    public static MaterialTheme Load()
    {
        var page = ThemeModelLoader.LoadPage(EmbeddedAsset.ReadBytes);
        var partials = ThemeModelLoader.LoadStandardPartials(EmbeddedAsset.ReadBytes);
        var assets = ThemeModelLoader.LoadStaticAssets(StaticAssetPaths, EmbeddedAsset.ReadBytes);
        return new(page, partials, assets);
    }
}
