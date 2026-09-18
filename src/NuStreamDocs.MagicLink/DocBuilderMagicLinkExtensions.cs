// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.MagicLink;

/// <summary>Builder-extension surface for the magic-link plugin.</summary>
public static class DocBuilderMagicLinkExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="MagicLinkPlugin"/> with default (URL-only) settings.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMagicLink() => builder.UsePlugin(new MagicLinkPlugin());

        /// <summary>Registers <see cref="MagicLinkPlugin"/> with the supplied options.</summary>
        /// <param name="options">Options controlling GitHub-shortref expansion.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMagicLink(MagicLinkOptions options) =>
            builder.UsePlugin(new MagicLinkPlugin(options));
    }
}
