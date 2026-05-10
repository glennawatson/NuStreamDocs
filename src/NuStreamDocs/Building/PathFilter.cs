// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.FileSystemGlobbing;
using NuStreamDocs.Common;

namespace NuStreamDocs.Building;

/// <summary>
/// Include/exclude glob filter applied during page discovery. Globs are forward-slashed,
/// docs-root-relative, and follow gitignore-style double-star semantics. With no includes
/// configured, every path passes (only excludes apply).
/// </summary>
public sealed class PathFilter
{
    /// <summary>The configured matcher.</summary>
    private readonly Matcher _matcher;

    /// <summary>Initializes a new instance of the <see cref="PathFilter"/> class.</summary>
    /// <param name="includes">Globs whose matches are kept; empty means "everything".</param>
    /// <param name="excludes">Globs whose matches are dropped; empty means "nothing dropped".</param>
    public PathFilter(GlobPattern[] includes, GlobPattern[] excludes)
    {
        ArgumentNullException.ThrowIfNull(includes);
        ArgumentNullException.ThrowIfNull(excludes);

        Matcher matcher = new(StringComparison.Ordinal);
        if (includes is [_, ..])
        {
            for (var i = 0; i < includes.Length; i++)
            {
                matcher.AddInclude(includes[i].Value);
            }
        }
        else
        {
            // Matcher requires at least one include to ever produce a hit;
            // "**/*" stands in as "everything under the root".
            matcher.AddInclude("**/*");
        }

        for (var i = 0; i < excludes.Length; i++)
        {
            matcher.AddExclude(excludes[i].Value);
        }

        _matcher = matcher;
        HasRules = includes is [_, ..] || excludes is [_, ..];
    }

    /// <summary>Gets a permissive filter that keeps every path.</summary>
    public static PathFilter Empty { get; } = new([], []);

    /// <summary>Gets a value indicating whether any glob has been configured. When false, callers may skip the matcher entirely.</summary>
    public bool HasRules { get; }

    /// <summary>Tests whether <paramref name="relativePath"/> survives the filter.</summary>
    /// <param name="relativePath">Forward-slashed path relative to the docs root.</param>
    /// <returns>True when the path is kept by the configured globs.</returns>
    public bool Matches(in FilePath relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath.Value);
        return _matcher.Match(relativePath.Value).HasMatches;
    }
}
