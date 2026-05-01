// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Tags;

/// <summary>Builder-extension surface for the tags plugin.</summary>
public static class DocBuilderTagsExtensions
{
    /// <summary>Registers <see cref="TagsPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseTags(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new TagsPlugin());
    }

    /// <summary>Registers <see cref="TagsPlugin"/> with caller-supplied options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Tags-plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseTags(this DocBuilder builder, in TagsOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new TagsPlugin(options));
    }
}
