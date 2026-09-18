// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Building;

/// <summary>
/// Adds caller-supplied stylesheet and script assets to every page (mkdocs-material's
/// <c>extra_css</c> / <c>extra_javascript</c> equivalents). Emission order matches registration
/// order.
/// </summary>
public static class DocBuilderExtraAssetsExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Adds an extra stylesheet from a file on disk; copied to <c>assets/extra/&lt;filename&gt;</c>.</summary>
        /// <param name="filePath">Absolute or relative path to a CSS file.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraCss(in FilePath filePath)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddCss(ExtraAssetSource.File(filePath));
            return builder;
        }

        /// <summary>Adds one or more extra stylesheets from files on disk.</summary>
        /// <param name="filePaths">Absolute or relative paths to CSS files.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraCss(params ReadOnlySpan<string> filePaths)
        {
            var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
            for (var i = 0; i < filePaths.Length; i++)
            {
                plugin.AddCss(ExtraAssetSource.File(filePaths[i]));
            }

            return builder;
        }

        /// <summary>Adds an inline UTF-8 stylesheet under a chosen output filename.</summary>
        /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
        /// <param name="utf8Css">UTF-8 CSS bytes.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraCssInline(in FilePath outputName, byte[] utf8Css)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddCss(ExtraAssetSource.Inline(outputName, utf8Css));
            return builder;
        }

        /// <summary>Adds an embedded-resource stylesheet from <paramref name="assembly"/>.</summary>
        /// <param name="assembly">Assembly carrying the resource.</param>
        /// <param name="resourceName">Manifest resource name.</param>
        /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraCssEmbedded(
            Assembly assembly,
            in ApiCompatString resourceName,
            in FilePath outputName)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>()
                .AddCss(ExtraAssetSource.Embedded(assembly, resourceName, outputName));
            return builder;
        }

        /// <summary>References an external stylesheet by URL; emits a <c>&lt;link&gt;</c> tag without shipping any asset.</summary>
        /// <param name="url">External href.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraCssLink(in UrlPath url)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddCss(ExtraAssetSource.External(url));
            return builder;
        }

        /// <summary>References one or more external stylesheets by URL.</summary>
        /// <param name="urls">External hrefs.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraCssLink(params ReadOnlySpan<UrlPath> urls)
        {
            var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
            for (var i = 0; i < urls.Length; i++)
            {
                plugin.AddCss(ExtraAssetSource.External(urls[i]));
            }

            return builder;
        }

        /// <summary>Adds an extra script from a file on disk; copied to <c>assets/extra/&lt;filename&gt;</c>.</summary>
        /// <param name="filePath">Absolute or relative path to a JS file.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJs(in FilePath filePath)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.File(filePath));
            return builder;
        }

        /// <summary>Adds one or more extra scripts from files on disk.</summary>
        /// <param name="filePaths">Absolute or relative paths to JS files.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJs(params ReadOnlySpan<string> filePaths)
        {
            var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
            for (var i = 0; i < filePaths.Length; i++)
            {
                plugin.AddJs(ExtraAssetSource.File(filePaths[i]));
            }

            return builder;
        }

        /// <summary>Adds an inline UTF-8 script under a chosen output filename.</summary>
        /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
        /// <param name="utf8Js">UTF-8 JS bytes.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsInline(in FilePath outputName, byte[] utf8Js)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.Inline(outputName, utf8Js));
            return builder;
        }

        /// <summary>Adds an embedded-resource script from <paramref name="assembly"/>.</summary>
        /// <param name="assembly">Assembly carrying the resource.</param>
        /// <param name="resourceName">Manifest resource name.</param>
        /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsEmbedded(
            Assembly assembly,
            in ApiCompatString resourceName,
            in FilePath outputName)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>()
                .AddJs(ExtraAssetSource.Embedded(assembly, resourceName, outputName));
            return builder;
        }

        /// <summary>Adds an extra ES-module script from a file on disk; emitted as <c>&lt;script type="module"&gt;</c>.</summary>
        /// <param name="filePath">Absolute or relative path to a JS module file.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsModule(in FilePath filePath)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.File(filePath).AsModule());
            return builder;
        }

        /// <summary>Adds one or more ES-module scripts from files on disk.</summary>
        /// <param name="filePaths">Absolute or relative paths to JS module files.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsModule(params ReadOnlySpan<string> filePaths)
        {
            var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
            for (var i = 0; i < filePaths.Length; i++)
            {
                plugin.AddJs(ExtraAssetSource.File(filePaths[i]).AsModule());
            }

            return builder;
        }

        /// <summary>Adds an inline UTF-8 ES-module script under a chosen output filename.</summary>
        /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
        /// <param name="utf8Js">UTF-8 JS bytes.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsModuleInline(in FilePath outputName, byte[] utf8Js)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.Inline(outputName, utf8Js).AsModule());
            return builder;
        }

        /// <summary>Adds an embedded-resource ES-module script from <paramref name="assembly"/>.</summary>
        /// <param name="assembly">Assembly carrying the resource.</param>
        /// <param name="resourceName">Manifest resource name.</param>
        /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsModuleEmbedded(
            Assembly assembly,
            in ApiCompatString resourceName,
            in FilePath outputName)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>()
                .AddJs(ExtraAssetSource.Embedded(assembly, resourceName, outputName).AsModule());
            return builder;
        }

        /// <summary>References an external ES-module script by URL.</summary>
        /// <param name="url">External src.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsModuleLink(in UrlPath url)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.External(url).AsModule());
            return builder;
        }

        /// <summary>References an external script by URL; emits a <c>&lt;script&gt;</c> tag without shipping any asset.</summary>
        /// <param name="url">External src.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsLink(in UrlPath url)
        {
            builder.GetOrAddPlugin<ExtraAssetsPlugin>().AddJs(ExtraAssetSource.External(url));
            return builder;
        }

        /// <summary>References one or more external scripts by URL.</summary>
        /// <param name="urls">External srcs.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder AddExtraJsLink(params ReadOnlySpan<UrlPath> urls)
        {
            var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
            for (var i = 0; i < urls.Length; i++)
            {
                plugin.AddJs(ExtraAssetSource.External(urls[i]));
            }

            return builder;
        }
    }
}
