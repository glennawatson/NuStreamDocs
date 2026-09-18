// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Nav;

/// <summary>Construction helpers for <see cref="NavOptions"/>'s glob-pattern lists.</summary>
public static class NavOptionsExtensions
{
    /// <summary>Extension members for <c>NavOptions</c>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in NavOptions options)
    {
        /// <summary>Replaces the include list with <paramref name="patterns"/>.</summary>
        /// <param name="patterns">Glob include patterns.</param>
        /// <returns>The updated options.</returns>
        public NavOptions WithIncludes(params GlobPattern[] patterns) =>
            options with { Includes = patterns };

        /// <summary>Appends <paramref name="patterns"/> to the existing include list.</summary>
        /// <param name="patterns">Additional glob patterns.</param>
        /// <returns>The updated options.</returns>
        public NavOptions AddIncludes(params GlobPattern[] patterns) =>
            patterns.Length is 0
                ? options
                : options with { Includes = ArrayJoiner.Concat(options.Includes, patterns) };

        /// <summary>Empties the include list.</summary>
        /// <returns>The updated options.</returns>
        public NavOptions ClearIncludes() =>
            options with { Includes = [] };

        /// <summary>Replaces the exclude list with <paramref name="patterns"/>.</summary>
        /// <param name="patterns">Glob exclude patterns.</param>
        /// <returns>The updated options.</returns>
        public NavOptions WithExcludes(params GlobPattern[] patterns) =>
            options with { Excludes = patterns };

        /// <summary>Appends <paramref name="patterns"/> to the existing exclude list.</summary>
        /// <param name="patterns">Additional glob patterns.</param>
        /// <returns>The updated options.</returns>
        public NavOptions AddExcludes(params GlobPattern[] patterns) =>
            patterns.Length is 0
                ? options
                : options with { Excludes = ArrayJoiner.Concat(options.Excludes, patterns) };

        /// <summary>Empties the exclude list.</summary>
        /// <returns>The updated options.</returns>
        public NavOptions ClearExcludes() =>
            options with { Excludes = [] };

        /// <summary>Replaces the curated nav list with <paramref name="entries"/>.</summary>
        /// <param name="entries">Curated entry tree.</param>
        /// <returns>The updated options.</returns>
        public NavOptions WithCuratedEntries(NavEntry[] entries) =>
            options with { CuratedEntries = entries };

        /// <summary>Empties the curated entry list, falling back to filesystem auto-discovery.</summary>
        /// <returns>The updated options.</returns>
        public NavOptions ClearCuratedEntries() =>
            options with { CuratedEntries = [] };
    }
}
