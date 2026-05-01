// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Building;

/// <summary>
/// Streams <see cref="PageWorkItem"/> descriptors from a docs root.
/// </summary>
/// <remarks>
/// Backed by <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/>,
/// which yields entries lazily so the discover stage never materialises
/// the full file list — even on large projects memory stays flat.
/// Filtering / glob exclude is applied per-item
/// during the walk, again to avoid an intermediate list.
/// </remarks>
public static class PageDiscovery
{
    /// <summary>Markdown extension the discovery walker recognises.</summary>
    private const string MarkdownExtension = ".md";

    /// <summary>Enumerates markdown pages under <paramref name="inputRoot"/> without cancellation support.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <returns>An async stream of <see cref="PageWorkItem"/>s.</returns>
    public static IAsyncEnumerable<PageWorkItem> EnumerateAsync(string inputRoot) =>
        EnumerateAsync(inputRoot, PathFilter.Empty, CancellationToken.None);

    /// <summary>Enumerates with cancellation support and no path filter.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of <see cref="PageWorkItem"/>s.</returns>
    public static IAsyncEnumerable<PageWorkItem> EnumerateAsync(
        string inputRoot,
        in CancellationToken cancellationToken) =>
        EnumerateAsync(inputRoot, PathFilter.Empty, cancellationToken);

    /// <summary>Enumerates with a path filter and cancellation support.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="filter">Include/exclude glob filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of <see cref="PageWorkItem"/>s.</returns>
    public static IAsyncEnumerable<PageWorkItem> EnumerateAsync(
        string inputRoot,
        PathFilter filter,
        in CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputRoot);
        ArgumentNullException.ThrowIfNull(filter);
        return EnumerateCoreAsync(inputRoot, filter, cancellationToken);
    }

    /// <summary>Iterator body for the public <c>EnumerateAsync</c> overloads.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="filter">Include/exclude glob filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of <see cref="PageWorkItem"/>s.</returns>
    private static async IAsyncEnumerable<PageWorkItem> EnumerateCoreAsync(
        string inputRoot,
        PathFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Directory.Exists(inputRoot))
        {
            yield break;
        }

        // On Linux/macOS the relative path comes back already separated by
        // '/', so the `.Replace('\\', '/')` allocates a fresh string for
        // nothing. Branch once at the top so the hot loop stays alloc-free
        // on Unix and only pays the swap on Windows.
        var needsSeparatorSwap = Path.DirectorySeparatorChar != '/';

        var enumerator = Directory.EnumerateFiles(
            inputRoot,
            "*" + MarkdownExtension,
            SearchOption.AllDirectories).GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = enumerator.Current;
                var rawRelative = Path.GetRelativePath(inputRoot, path);
                var relative = needsSeparatorSwap
                    ? rawRelative.Replace('\\', '/')
                    : rawRelative;

                // Hot-path short-circuit: when the builder didn't
                // configure any globs, skip the matcher entirely.
                if (filter.HasRules && !filter.Matches(relative))
                {
                    continue;
                }

                var flags = FrontmatterFlagReader.Read(path);
                yield return new(path, relative, flags);

                // Yield to the scheduler periodically so a watcher
                // session doesn't hog a thread on a large initial walk.
                await Task.Yield();
            }
        }
        finally
        {
            enumerator.Dispose();
        }
    }
}
