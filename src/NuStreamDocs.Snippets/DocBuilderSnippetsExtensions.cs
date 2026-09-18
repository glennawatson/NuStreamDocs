// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;
using NuStreamDocs.Common;

namespace NuStreamDocs.Snippets;

/// <summary>Builder-extension surface for the snippets plugin.</summary>
public static class DocBuilderSnippetsExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="SnippetsPlugin"/> using the build's docs root as the snippet base.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSnippets() => builder.UsePlugin(new SnippetsPlugin());

        /// <summary>Registers <see cref="SnippetsPlugin"/> with a caller-supplied base directory for snippet resolution.</summary>
        /// <param name="baseDirectory">Absolute path under which snippet includes resolve.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseSnippets(in DirectoryPath baseDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory.Value);
            return builder.UsePlugin(new SnippetsPlugin(baseDirectory));
        }
    }
}
