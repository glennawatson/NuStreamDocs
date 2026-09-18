// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Tags;

/// <summary>Builder-extension surface for the tags plugin.</summary>
public static class DocBuilderTagsExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="TagsPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseTags() => builder.UsePlugin(new TagsPlugin());

        /// <summary>Registers <see cref="TagsPlugin"/> with caller-supplied options.</summary>
        /// <param name="options">Tags-plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseTags(in TagsOptions options) =>
            builder.UsePlugin(new TagsPlugin(options));
    }
}
