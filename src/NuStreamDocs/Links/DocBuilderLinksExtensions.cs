// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Links;

/// <summary>Builder-extension surface for the link-rewriter plugins.</summary>
public static class DocBuilderLinksExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="MarkdownLinkRewriterPlugin"/> with config-driven directory-URL behavior.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMarkdownLinks() =>
            builder.UsePlugin(new MarkdownLinkRewriterPlugin());

        /// <summary>Registers <see cref="MarkdownLinkRewriterPlugin"/> with an explicit directory-URL toggle that overrides the config.</summary>
        /// <param name="useDirectoryUrls">True for <c>foo/</c> targets; false for <c>foo.html</c>.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMarkdownLinks(bool useDirectoryUrls) =>
            builder.UsePlugin(new MarkdownLinkRewriterPlugin(useDirectoryUrls));
    }
}
