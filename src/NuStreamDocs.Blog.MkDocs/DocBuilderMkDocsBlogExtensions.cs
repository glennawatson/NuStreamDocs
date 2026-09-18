// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Blog.MkDocs;

/// <summary>Builder extensions that register the mkdocs-material blog plugin.</summary>
public static class DocBuilderMkDocsBlogExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder receiving the blog plugin.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="MkDocsBlogPlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMkDocsBlog(MkDocsBlogOptions options) =>
            builder.UsePlugin(new MkDocsBlogPlugin(options));

        /// <summary>Registers <see cref="MkDocsBlogPlugin"/> with options and a logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMkDocsBlog(MkDocsBlogOptions options, ILogger logger) =>
            builder.UsePlugin(new MkDocsBlogPlugin(options, logger));
    }
}
