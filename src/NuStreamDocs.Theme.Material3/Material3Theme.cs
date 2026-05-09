// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using NuStreamDocs.Common;
using NuStreamDocs.Templating;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3;

/// <summary>Material Design 3 theme: compiled page + partial templates plus the bundled CSS / JS assets.</summary>
public sealed class Material3Theme : IThemePackage
{
    /// <summary>Static asset paths shipped by the theme.</summary>
    private static readonly FilePath[] StaticAssetPaths =
    [
        "assets/stylesheets/material3.css",
        "assets/javascripts/material3.js",
        "assets/images/favicon.svg"
    ];

    /// <summary>Static assets as an indexable snapshot for write-out loops.</summary>
    private readonly (FilePath RelativePath, byte[] Bytes)[] _staticAssetEntries;

    /// <summary>Initializes a new instance of the <see cref="Material3Theme"/> class.</summary>
    /// <param name="page">Compiled top-level page template.</param>
    /// <param name="partials">Compiled partial templates.</param>
    /// <param name="staticAssets">Static-asset bundle keyed by relative path.</param>
    private Material3Theme(Template page, Dictionary<byte[], Template> partials, Dictionary<FilePath, byte[]> staticAssets)
    {
        Page = page;
        Partials = partials;
        StaticAssets = new(staticAssets);
        _staticAssetEntries = ThemeModelLoader.BuildStaticAssetEntries(staticAssets);
    }

    /// <summary>Gets the compiled top-level page template.</summary>
    public Template Page { get; }

    /// <summary>Gets the compiled partial registry, keyed by UTF-8 partial-name bytes.</summary>
    public Dictionary<byte[], Template> Partials { get; }

    /// <summary>Gets the static-asset map, keyed by relative output path.</summary>
    public ReadOnlyDictionary<FilePath, byte[]> StaticAssets { get; }

    /// <inheritdoc/>
    (FilePath RelativePath, byte[] Bytes)[] IThemePackage.StaticAssetEntries => _staticAssetEntries;

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
