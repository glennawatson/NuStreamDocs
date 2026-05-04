// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Common;

namespace NuStreamDocs.Snippets;

/// <summary>Builder-extension surface for the snippets plugin.</summary>
public static class DocBuilderSnippetsExtensions
{
    /// <summary>Registers <see cref="SnippetsPlugin"/> using the build's docs root as the snippet base.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSnippets(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new SnippetsPlugin());
    }

    /// <summary>Registers <see cref="SnippetsPlugin"/> with a caller-supplied base directory for snippet resolution.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="baseDirectory">Absolute path under which snippet includes resolve.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSnippets(this DocBuilder builder, DirectoryPath baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory.Value);
        return builder.UsePlugin(new SnippetsPlugin(baseDirectory));
    }
}
