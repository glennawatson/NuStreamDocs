// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Nav;

/// <summary>Construction helpers for <see cref="NavOptions"/>'s glob-pattern lists.</summary>
/// <remarks>
/// <see cref="NavOptions.Includes"/> / <see cref="NavOptions.Excludes"/> use
/// <see cref="GlobPattern"/> so the "this is a glob, not a path" intent reads from the type.
/// String literals at call sites convert via the implicit operator, so existing fluent calls
/// (e.g. <c>opts.WithExcludes("articles/**")</c>) keep compiling unchanged.
/// </remarks>
public static class NavOptionsExtensions
{
    /// <summary>Replaces the include list with <paramref name="patterns"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Glob include patterns.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions WithIncludes(this NavOptions options, params GlobPattern[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return options with { Includes = patterns };
    }

    /// <summary>Appends <paramref name="patterns"/> to the existing include list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Additional glob patterns.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions AddIncludes(this NavOptions options, params GlobPattern[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return patterns.Length is 0
            ? options
            : options with { Includes = ArrayJoiner.Concat(options.Includes, patterns) };
    }

    /// <summary>Empties the include list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions ClearIncludes(this NavOptions options) =>
        options with { Includes = [] };

    /// <summary>Replaces the exclude list with <paramref name="patterns"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Glob exclude patterns.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions WithExcludes(this NavOptions options, params GlobPattern[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return options with { Excludes = patterns };
    }

    /// <summary>Appends <paramref name="patterns"/> to the existing exclude list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="patterns">Additional glob patterns.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions AddExcludes(this NavOptions options, params GlobPattern[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return patterns.Length is 0
            ? options
            : options with { Excludes = ArrayJoiner.Concat(options.Excludes, patterns) };
    }

    /// <summary>Empties the exclude list.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions ClearExcludes(this NavOptions options) =>
        options with { Excludes = [] };

    /// <summary>Replaces the curated nav list with <paramref name="entries"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="entries">Curated entry tree (mkdocs.yml, docfx <c>toc.yml</c>, awesome-nav, etc.).</param>
    /// <returns>The updated options.</returns>
    /// <remarks>
    /// Reader assemblies expose their own <c>From*</c> extensions on top of this primitive — see
    /// <c>NuStreamDocs.Config.MkDocs</c> for <c>FromMkDocsYaml</c> and <c>NuStreamDocs.Config.DocFx</c>
    /// for <c>FromDocFxTocs</c>.
    /// </remarks>
    public static NavOptions WithCuratedEntries(this NavOptions options, NavEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return options with { CuratedEntries = entries };
    }

    /// <summary>Empties the curated entry list, falling back to filesystem auto-discovery.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions ClearCuratedEntries(this NavOptions options) =>
        options with { CuratedEntries = [] };
}
