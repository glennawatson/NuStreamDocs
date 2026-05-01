// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Links;

/// <summary>Builder-extension surface for the link-rewriter plugins.</summary>
public static class DocBuilderLinksExtensions
{
    /// <summary>Registers <see cref="MarkdownLinkRewriterPlugin"/> with config-driven directory-URL behaviour.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMarkdownLinks(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new MarkdownLinkRewriterPlugin());
    }

    /// <summary>Registers <see cref="MarkdownLinkRewriterPlugin"/> with an explicit directory-URL toggle that overrides the config.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="useDirectoryUrls">True for <c>foo/</c> targets; false for <c>foo.html</c>.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMarkdownLinks(this DocBuilder builder, bool useDirectoryUrls)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new MarkdownLinkRewriterPlugin(useDirectoryUrls));
    }
}
