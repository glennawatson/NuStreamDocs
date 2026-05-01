// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Sitemap;

/// <summary>Builder-extension surface for the sitemap / 404 / redirects plugins.</summary>
public static class DocBuilderSitemapExtensions
{
    /// <summary>Registers <see cref="SitemapPlugin"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSitemap(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new SitemapPlugin());
    }

    /// <summary>Registers <see cref="NotFoundPlugin"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseNotFoundPage(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new NotFoundPlugin());
    }

    /// <summary>Registers <see cref="RedirectsPlugin"/> with the given <paramref name="entries"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="entries">Tuples of <c>(fromPath, toUrl)</c>.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseRedirects(this DocBuilder builder, params (string From, string To)[] entries)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(entries);
        return builder.UsePlugin(new RedirectsPlugin(entries));
    }

    /// <summary>Registers <see cref="RedirectsPlugin"/> with explicit options and static entries.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options controlling config-file lookup and frontmatter alias scanning.</param>
    /// <param name="entries">Static tuples of <c>(fromPath, toUrl)</c>.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseRedirects(this DocBuilder builder, in RedirectsOptions options, params (string From, string To)[] entries)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(entries);
        return builder.UsePlugin(new RedirectsPlugin(options, entries));
    }
}
