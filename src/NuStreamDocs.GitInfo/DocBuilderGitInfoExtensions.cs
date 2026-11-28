// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.GitInfo;

/// <summary>Builder-extension surface for <see cref="GitInfoPlugin"/>.</summary>
public static class DocBuilderGitInfoExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="GitInfoPlugin"/> with <see cref="GitInfoOptions.Default"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseGitInfo() =>
            builder.UsePlugin(new GitInfoPlugin());

        /// <summary>Registers <see cref="GitInfoPlugin"/> with the supplied options.</summary>
        /// <param name="options">Git info options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseGitInfo(in GitInfoOptions options) =>
            builder.UsePlugin(new GitInfoPlugin(options));

        /// <summary>Registers <see cref="GitInfoPlugin"/> with options produced from <see cref="GitInfoOptions.Default"/>.</summary>
        /// <param name="configure">Callback receiving <see cref="GitInfoOptions.Default"/> and returning customized options.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseGitInfo(Func<GitInfoOptions, GitInfoOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new GitInfoPlugin(configure(GitInfoOptions.Default)));
        }
    }
}
