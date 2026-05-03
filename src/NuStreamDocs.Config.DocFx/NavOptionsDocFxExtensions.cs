// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Nav;

namespace NuStreamDocs.Config.DocFx;

/// <summary>
/// Fluent extensions that load a curated nav tree from docfx-style <c>toc.yml</c> files into
/// <see cref="NavOptions.CuratedEntries"/>. Keeps docfx-specific serialization isolated from
/// <c>NuStreamDocs.Nav</c> so the core nav module never references a config dialect.
/// </summary>
public static class NavOptionsDocFxExtensions
{
    /// <summary>Reads <paramref name="rootDirectory"/>'s <c>toc.yml</c> recursively, resolving sub-tocs and homepages, and returns options with the curated list populated.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="rootDirectory">Absolute path to the directory holding the root <c>toc.yml</c>.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions FromDocFxTocs(this NavOptions options, string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return options.WithCuratedEntries(DocFxTocReader.ReadTree(rootDirectory));
    }
}
