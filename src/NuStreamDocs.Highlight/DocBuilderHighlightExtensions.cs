// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Highlight;

/// <summary>Builder extension that registers <see cref="HighlightPlugin"/>.</summary>
public static class DocBuilderHighlightExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder"></param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers the highlighter with default options (built-in lexers only).</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseHighlight() => builder.UsePlugin(new HighlightPlugin());

        /// <summary>Registers the highlighter with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseHighlight(HighlightOptions options) =>
            builder.UsePlugin(new HighlightPlugin(options));
    }
}
