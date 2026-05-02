// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using NuStreamDocs.Templating;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3;

/// <summary>
/// Material Design 3 theme: pre-compiled page + partial templates plus
/// the hand-written stylesheet and palette-toggle JS.
/// </summary>
public sealed class Material3Theme : IThemePackage
{
    /// <summary>Embedded asset paths the theme ships.</summary>
    private static readonly string[] StaticAssetPaths =
    [
        "assets/stylesheets/material3.css",
        "assets/javascripts/material3.js",
    ];

    /// <summary>Static assets as an indexable snapshot for write-out loops.</summary>
    private readonly (string RelativePath, byte[] Bytes)[] _staticAssetEntries;

    /// <summary>Initializes a new instance of the <see cref="Material3Theme"/> class.</summary>
    /// <param name="page">Compiled top-level page template.</param>
    /// <param name="partials">Compiled partial templates.</param>
    /// <param name="staticAssets">Static-asset bundle keyed by relative path.</param>
    private Material3Theme(Template page, Dictionary<string, Template> partials, Dictionary<string, byte[]> staticAssets)
    {
        Page = page;
        Partials = partials;
        StaticAssets = new(staticAssets);
        _staticAssetEntries = ThemeModelLoader.BuildStaticAssetEntries(staticAssets);
    }

    /// <summary>Gets the compiled top-level page template.</summary>
    public Template Page { get; }

    /// <summary>Gets the compiled partial registry, keyed by partial name. Built once at theme load and probed repeatedly across page renders, so freezing pays off here.</summary>
    public Dictionary<string, Template> Partials { get; }

    /// <summary>
    /// Gets the static-asset map, keyed by relative output path. Plain
    /// <see cref="Dictionary{TKey, TValue}"/> exposed read-only — the bundle is tiny
    /// (2-3 entries) and the freeze cost wouldn't repay itself.
    /// </summary>
    public ReadOnlyDictionary<string, byte[]> StaticAssets { get; }

    /// <inheritdoc/>
    (string RelativePath, byte[] Bytes)[] IThemePackage.StaticAssetEntries => _staticAssetEntries;

    /// <summary>Loads and compiles the theme from its embedded resources.</summary>
    /// <returns>A ready-to-use <see cref="Material3Theme"/>.</returns>
    public static Material3Theme Load()
    {
        var page = ThemeModelLoader.LoadPage(EmbeddedAsset.ReadBytes);
        var partials = ThemeModelLoader.LoadStandardPartials(EmbeddedAsset.ReadBytes);
        var assets = ThemeModelLoader.LoadStaticAssets(StaticAssetPaths, EmbeddedAsset.ReadBytes);
        return new(page, partials, assets);
    }
}
