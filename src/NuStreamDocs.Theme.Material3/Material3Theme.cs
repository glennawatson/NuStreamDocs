// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using NuStreamDocs.Common;
using NuStreamDocs.Templating;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3;

/// <summary>
/// Material Design 3 theme: pre-compiled page + partial templates plus
/// the bundled theme assets and vendored official Material Web runtime subset.
/// </summary>
public sealed class Material3Theme : IThemePackage
{
    /// <summary>Embedded asset paths the theme ships.</summary>
    private static readonly string[] StaticAssetPaths =
    [
        "assets/stylesheets/material3.css",
        "assets/javascripts/material3.js",
        "assets/javascripts/material-web-init.js",
        "assets/vendor/@lit/reactive-element/css-tag.js",
        "assets/vendor/@lit/reactive-element/decorators/base.js",
        "assets/vendor/@lit/reactive-element/decorators/custom-element.js",
        "assets/vendor/@lit/reactive-element/decorators/event-options.js",
        "assets/vendor/@lit/reactive-element/decorators/property.js",
        "assets/vendor/@lit/reactive-element/decorators/query-all.js",
        "assets/vendor/@lit/reactive-element/decorators/query-assigned-elements.js",
        "assets/vendor/@lit/reactive-element/decorators/query-assigned-nodes.js",
        "assets/vendor/@lit/reactive-element/decorators/query-async.js",
        "assets/vendor/@lit/reactive-element/decorators/query.js",
        "assets/vendor/@lit/reactive-element/decorators/state.js",
        "assets/vendor/@lit/reactive-element/reactive-element.js",
        "assets/vendor/@lit-labs/ssr-dom-shim/index.js",
        "assets/vendor/@material/web/field/internal/field.js",
        "assets/vendor/@material/web/field/internal/outlined-field.js",
        "assets/vendor/@material/web/field/internal/outlined-styles.js",
        "assets/vendor/@material/web/field/internal/shared-styles.js",
        "assets/vendor/@material/web/field/outlined-field.js",
        "assets/vendor/@material/web/focus/internal/focus-ring-styles.js",
        "assets/vendor/@material/web/focus/internal/focus-ring.js",
        "assets/vendor/@material/web/focus/md-focus-ring.js",
        "assets/vendor/@material/web/iconbutton/icon-button.js",
        "assets/vendor/@material/web/iconbutton/internal/icon-button.js",
        "assets/vendor/@material/web/iconbutton/internal/shared-styles.js",
        "assets/vendor/@material/web/iconbutton/internal/standard-styles.js",
        "assets/vendor/@material/web/internal/aria/aria.js",
        "assets/vendor/@material/web/internal/aria/delegate.js",
        "assets/vendor/@material/web/internal/controller/attachable-controller.js",
        "assets/vendor/@material/web/internal/controller/form-submitter.js",
        "assets/vendor/@material/web/internal/controller/is-rtl.js",
        "assets/vendor/@material/web/internal/controller/string-converter.js",
        "assets/vendor/@material/web/internal/events/redispatch-event.js",
        "assets/vendor/@material/web/internal/motion/animation.js",
        "assets/vendor/@material/web/labs/behaviors/constraint-validation.js",
        "assets/vendor/@material/web/labs/behaviors/element-internals.js",
        "assets/vendor/@material/web/labs/behaviors/form-associated.js",
        "assets/vendor/@material/web/labs/behaviors/on-report-validity.js",
        "assets/vendor/@material/web/labs/behaviors/validators/text-field-validator.js",
        "assets/vendor/@material/web/labs/behaviors/validators/validator.js",
        "assets/vendor/@material/web/ripple/internal/ripple-styles.js",
        "assets/vendor/@material/web/ripple/internal/ripple.js",
        "assets/vendor/@material/web/ripple/ripple.js",
        "assets/vendor/@material/web/textfield/internal/outlined-styles.js",
        "assets/vendor/@material/web/textfield/internal/outlined-text-field.js",
        "assets/vendor/@material/web/textfield/internal/shared-styles.js",
        "assets/vendor/@material/web/textfield/internal/text-field.js",
        "assets/vendor/@material/web/textfield/outlined-text-field.js",
        "assets/vendor/lit/decorators.js",
        "assets/vendor/lit/directives/class-map.js",
        "assets/vendor/lit/directives/live.js",
        "assets/vendor/lit/directives/style-map.js",
        "assets/vendor/lit/index.js",
        "assets/vendor/lit/static-html.js",
        "assets/vendor/lit-element/lit-element.js",
        "assets/vendor/lit-html/directive-helpers.js",
        "assets/vendor/lit-html/directive.js",
        "assets/vendor/lit-html/directives/class-map.js",
        "assets/vendor/lit-html/directives/live.js",
        "assets/vendor/lit-html/directives/style-map.js",
        "assets/vendor/lit-html/is-server.js",
        "assets/vendor/lit-html/lit-html.js",
        "assets/vendor/lit-html/static.js",
        "assets/vendor/tslib/tslib.es6.mjs",
    ];

    /// <summary>Static assets as an indexable snapshot for write-out loops.</summary>
    private readonly (FilePath RelativePath, byte[] Bytes)[] _staticAssetEntries;

    /// <summary>Initializes a new instance of the <see cref="Material3Theme"/> class.</summary>
    /// <param name="page">Compiled top-level page template.</param>
    /// <param name="partials">Compiled partial templates.</param>
    /// <param name="staticAssets">Static-asset bundle keyed by relative path.</param>
    private Material3Theme(Template page, Dictionary<byte[], Template> partials, Dictionary<string, byte[]> staticAssets)
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
     /// <see cref="Dictionary{TKey, TValue}"/> exposed read-only.
     /// </summary>
    public ReadOnlyDictionary<string, byte[]> StaticAssets { get; }

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
