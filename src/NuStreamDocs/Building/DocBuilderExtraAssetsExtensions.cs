// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Building;

/// <summary>
/// Builder-extension surface for adding caller-supplied stylesheet and
/// script assets to every page (the equivalent of mkdocs-material's
/// <c>extra_css</c> and <c>extra_javascript</c> config keys).
/// </summary>
/// <remarks>
/// Repeated <c>AddExtraCss</c> / <c>AddExtraJs</c> calls fold onto a
/// single underlying <see cref="ExtraAssetsPlugin"/>; the order of
/// emitted <c>&lt;link&gt;</c> / <c>&lt;script&gt;</c> tags matches
/// registration order so consumers control cascade precedence.
/// </remarks>
public static class DocBuilderExtraAssetsExtensions
{
    /// <summary>Adds an extra stylesheet from a file on disk; copied to <c>assets/extra/&lt;filename&gt;</c>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="filePath">Absolute or relative path to a CSS file.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraCss(this DocBuilder builder, FilePath filePath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddCss(ExtraAssetSource.File(filePath));
        return builder;
    }

    /// <summary>Adds one or more extra stylesheets from files on disk.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="filePaths">Absolute or relative paths to CSS files.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraCss(this DocBuilder builder, params ReadOnlySpan<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
        for (var i = 0; i < filePaths.Length; i++)
        {
            plugin.AddCss(ExtraAssetSource.File(filePaths[i]));
        }

        return builder;
    }

    /// <summary>Adds an inline UTF-8 stylesheet under a chosen output filename.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
    /// <param name="utf8Css">UTF-8 CSS bytes.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraCssInline(this DocBuilder builder, string outputName, byte[] utf8Css)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddCss(ExtraAssetSource.Inline(outputName, utf8Css));
        return builder;
    }

    /// <summary>Adds an embedded-resource stylesheet from <paramref name="assembly"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="assembly">Assembly carrying the resource.</param>
    /// <param name="resourceName">Manifest resource name.</param>
    /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraCssEmbedded(this DocBuilder builder, Assembly assembly, string resourceName, string outputName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddCss(ExtraAssetSource.Embedded(assembly, resourceName, outputName));
        return builder;
    }

    /// <summary>References an external stylesheet by URL; emits a <c>&lt;link&gt;</c> tag without shipping any asset.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="url">External href.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraCssLink(this DocBuilder builder, string url)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddCss(ExtraAssetSource.External(url));
        return builder;
    }

    /// <summary>References one or more external stylesheets by URL.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="urls">External hrefs.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraCssLink(this DocBuilder builder, params ReadOnlySpan<string> urls)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
        for (var i = 0; i < urls.Length; i++)
        {
            plugin.AddCss(ExtraAssetSource.External(urls[i]));
        }

        return builder;
    }

    /// <summary>Adds an extra script from a file on disk; copied to <c>assets/extra/&lt;filename&gt;</c>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="filePath">Absolute or relative path to a JS file.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraJs(this DocBuilder builder, FilePath filePath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.File(filePath));
        return builder;
    }

    /// <summary>Adds one or more extra scripts from files on disk.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="filePaths">Absolute or relative paths to JS files.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraJs(this DocBuilder builder, params ReadOnlySpan<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
        for (var i = 0; i < filePaths.Length; i++)
        {
            plugin.AddJs(ExtraAssetSource.File(filePaths[i]));
        }

        return builder;
    }

    /// <summary>Adds an inline UTF-8 script under a chosen output filename.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
    /// <param name="utf8Js">UTF-8 JS bytes.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraJsInline(this DocBuilder builder, string outputName, byte[] utf8Js)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.Inline(outputName, utf8Js));
        return builder;
    }

    /// <summary>Adds an embedded-resource script from <paramref name="assembly"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="assembly">Assembly carrying the resource.</param>
    /// <param name="resourceName">Manifest resource name.</param>
    /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraJsEmbedded(this DocBuilder builder, Assembly assembly, string resourceName, string outputName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.Embedded(assembly, resourceName, outputName));
        return builder;
    }

    /// <summary>References an external script by URL; emits a <c>&lt;script&gt;</c> tag without shipping any asset.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="url">External src.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraJsLink(this DocBuilder builder, string url)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.External(url));
        return builder;
    }

    /// <summary>References one or more external scripts by URL.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="urls">External srcs.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder AddExtraJsLink(this DocBuilder builder, params ReadOnlySpan<string> urls)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
        for (var i = 0; i < urls.Length; i++)
        {
            plugin.AddJs(ExtraAssetSource.External(urls[i]));
        }

        return builder;
    }
}
