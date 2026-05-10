// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Nav;

/// <summary>Construction helpers for <see cref="NavOptions"/>'s glob-pattern lists.</summary>
public static class NavOptionsExtensions
{
    /// <summary>Replaces the include list with <paramref name="patterns"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Glob include patterns.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions WithIncludes(this in NavOptions options, params GlobPattern[] patterns)
    {
        return options with { Includes = patterns };
    }

    /// <summary>Appends <paramref name="patterns"/> to the existing include list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Additional glob patterns.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions AddIncludes(this in NavOptions options, params GlobPattern[] patterns)
    {
        return patterns.Length is 0
            ? options
            : options with { Includes = ArrayJoiner.Concat(options.Includes, patterns) };
    }

    /// <summary>Empties the include list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions ClearIncludes(this in NavOptions options) =>
        options with { Includes = [] };

    /// <summary>Replaces the exclude list with <paramref name="patterns"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Glob exclude patterns.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions WithExcludes(this in NavOptions options, params GlobPattern[] patterns)
    {
        return options with { Excludes = patterns };
    }

    /// <summary>Appends <paramref name="patterns"/> to the existing exclude list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Additional glob patterns.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions AddExcludes(this in NavOptions options, params GlobPattern[] patterns)
    {
        return patterns.Length is 0
            ? options
            : options with { Excludes = ArrayJoiner.Concat(options.Excludes, patterns) };
    }

    /// <summary>Empties the exclude list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions ClearExcludes(this in NavOptions options) =>
        options with { Excludes = [] };

    /// <summary>Replaces the curated nav list with <paramref name="entries"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="entries">Curated entry tree.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions WithCuratedEntries(this in NavOptions options, NavEntry[] entries)
    {
        return options with { CuratedEntries = entries };
    }

    /// <summary>Empties the curated entry list, falling back to filesystem auto-discovery.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions ClearCuratedEntries(this in NavOptions options) =>
        options with { CuratedEntries = [] };
}
