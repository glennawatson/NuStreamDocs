// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;
using NuStreamDocs.Common;

namespace NuStreamDocs.Sitemap;

/// <summary>Builder-extension surface for the sitemap / 404 / redirects plugins.</summary>
public static class DocBuilderSitemapExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="SitemapPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSitemap() => builder.UsePlugin(new SitemapPlugin());

        /// <summary>Registers <see cref="NotFoundPlugin"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseNotFoundPage() => builder.UsePlugin(new NotFoundPlugin());

        /// <summary>Registers <see cref="RedirectsPlugin"/> with the given <paramref name="entries"/>.</summary>
        /// <param name="entries">Tuples of <c>(fromPath, toUrl)</c>.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseRedirects(params (UrlPath From, UrlPath To)[] entries) =>
            builder.UsePlugin(new RedirectsPlugin(entries));

        /// <summary>Registers <see cref="RedirectsPlugin"/> with explicit options and static entries.</summary>
        /// <param name="options">Plugin options controlling config-file lookup and frontmatter alias scanning.</param>
        /// <param name="entries">Static tuples of <c>(fromPath, toUrl)</c>.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseRedirects(
            in RedirectsOptions options,
            params (UrlPath From, UrlPath To)[] entries) => builder.UsePlugin(new RedirectsPlugin(options, entries));
    }
}
