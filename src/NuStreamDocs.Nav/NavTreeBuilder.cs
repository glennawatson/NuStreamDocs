// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;
using NuStreamDocs.Nav.Logging;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Nav;

/// <summary>
/// Internal static helper that walks the input tree and produces the
/// in-memory <see cref="NavNode"/> hierarchy.
/// </summary>
/// <remarks>
/// One static method per concern: directory walk, glob filter,
/// ordering, override-file merging. Keeps allocations bounded by
/// using <see cref="ArrayPool{T}"/> rentals for transient buffers and
/// pre-sized arrays for the final tree.
/// </remarks>
internal static class NavTreeBuilder
{
    /// <summary>Markdown extension recognised when building the nav tree.</summary>
    private const string MarkdownExtension = ".md";

    /// <summary>File name of the literate-nav override (<c>.pages</c>) read from each section directory.</summary>
    private const string PagesFileName = ".pages";

    /// <summary>
    /// Builds the nav tree rooted at <paramref name="inputRoot"/>.
    /// </summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>Root <see cref="NavNode"/>; an empty section node when the root is missing.</returns>
    public static NavNode Build(string inputRoot, in NavOptions options) =>
        Build(inputRoot, in options, NullLogger.Instance);

    /// <summary>
    /// Builds the nav tree rooted at <paramref name="inputRoot"/> emitting log events to <paramref name="logger"/>.
    /// </summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger that receives start/complete events.</param>
    /// <returns>Root <see cref="NavNode"/>; an empty section node when the root is missing.</returns>
    public static NavNode Build(string inputRoot, in NavOptions options, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputRoot);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(inputRoot))
        {
            NavLoggingHelper.LogNavBuildStart(logger, inputRoot, 0);
            NavLoggingHelper.LogNavBuildComplete(logger, 0, 0, 0);
            return new(string.Empty, string.Empty, isSection: true, []);
        }

        var candidateCount = CountMarkdownFiles(inputRoot);
        NavLoggingHelper.LogNavBuildStart(logger, inputRoot, candidateCount);

        var matcher = BuildMatcher(in options);
        var root = BuildSection(inputRoot, inputRoot, matcher, in options, logger) ??
            new(string.Empty, string.Empty, isSection: true, []);

        var (sections, leaves) = TallyTree(root);
        var pruned = candidateCount - leaves;
        if (pruned < 0)
        {
            // index promotion can produce extra leaves; clamp the displayed pruned count.
            pruned = 0;
        }

        NavLoggingHelper.LogNavBuildComplete(logger, sections, leaves, pruned);

        if (options.WarnOnOrphanPages)
        {
            ReportOrphanPages(inputRoot, root, matcher, logger);
        }

        root.AttachParents();
        return root;
    }

    /// <summary>
    /// Compares the on-disk markdown files (filtered by <paramref name="matcher"/>) against the leaves of the
    /// rendered nav tree and logs any path that survives the matcher but doesn't appear in the tree.
    /// </summary>
    /// <param name="inputRoot">Absolute docs input root.</param>
    /// <param name="root">Built nav tree.</param>
    /// <param name="matcher">Glob matcher used for the nav build.</param>
    /// <param name="logger">Target logger.</param>
    private static void ReportOrphanPages(string inputRoot, NavNode root, Matcher matcher, ILogger logger)
    {
        var navPaths = CollectNavLeafPaths(root);
        var orphans = new List<string>();

        var matchResult = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new(inputRoot)));
        foreach (var file in matchResult.Files)
        {
            var rel = file.Path.Replace('\\', '/');
            if (!navPaths.Contains(rel))
            {
                orphans.Add(rel);
            }
        }

        if (orphans.Count is 0)
        {
            return;
        }

        orphans.Sort(StringComparer.Ordinal);
        NavLoggingHelper.LogOrphanPagesHeader(logger, orphans.Count);
        for (var i = 0; i < orphans.Count; i++)
        {
            NavLoggingHelper.LogOrphanPage(logger, orphans[i]);
        }
    }

    /// <summary>Walks <paramref name="root"/> and returns the relative path of every leaf reachable through the rendered nav.</summary>
    /// <param name="root">Built nav tree.</param>
    /// <returns>Set of leaf source-relative paths (forward-slash, ordinal).</returns>
    private static HashSet<string> CollectNavLeafPaths(NavNode root)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        CollectPaths(root, paths);
        return paths;
    }

    /// <summary>Recursive helper for <see cref="CollectNavLeafPaths"/>.</summary>
    /// <param name="node">Current node.</param>
    /// <param name="paths">Accumulator.</param>
    private static void CollectPaths(NavNode node, HashSet<string> paths)
    {
        if (!node.IsSection)
        {
            if (!string.IsNullOrEmpty(node.RelativePath))
            {
                paths.Add(node.RelativePath);
            }

            return;
        }

        if (!string.IsNullOrEmpty(node.IndexPath))
        {
            paths.Add(node.IndexPath);
        }

        var children = node.Children;
        for (var i = 0; i < children.Length; i++)
        {
            CollectPaths(children[i], paths);
        }
    }

    /// <summary>Counts every <c>.md</c> file under <paramref name="root"/> for the build-start log line.</summary>
    /// <param name="root">Absolute docs root.</param>
    /// <returns>Total markdown file count.</returns>
    private static int CountMarkdownFiles(string root)
    {
        var count = 0;
        var enumerable = Directory.EnumerateFiles(root, "*" + MarkdownExtension, SearchOption.AllDirectories);
        using var enumerator = enumerable.GetEnumerator();
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    /// <summary>Walks <paramref name="root"/> and returns the section + leaf counts.</summary>
    /// <param name="root">Built nav root.</param>
    /// <returns>Section and leaf counts.</returns>
    private static (int Sections, int Leaves) TallyTree(NavNode root)
    {
        var sections = 0;
        var leaves = 0;
        TallyNode(root, ref sections, ref leaves);
        return (sections, leaves);
    }

    /// <summary>Recursive walk that increments <paramref name="sections"/> and <paramref name="leaves"/>.</summary>
    /// <param name="node">Current node.</param>
    /// <param name="sections">Section accumulator.</param>
    /// <param name="leaves">Leaf accumulator.</param>
    private static void TallyNode(NavNode node, ref int sections, ref int leaves)
    {
        if (!node.IsSection)
        {
            leaves++;
            return;
        }

        sections++;
        var children = node.Children;
        for (var i = 0; i < children.Length; i++)
        {
            TallyNode(children[i], ref sections, ref leaves);
        }
    }

    /// <summary>Builds a glob matcher from the include/exclude lists.</summary>
    /// <param name="options">Plugin options.</param>
    /// <returns>A configured <see cref="Matcher"/>; matches all <c>.md</c> when no includes are given.</returns>
    private static Matcher BuildMatcher(in NavOptions options)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        if (options.Includes.Length == 0)
        {
            matcher.AddInclude("**/*" + MarkdownExtension);
        }
        else
        {
            for (var i = 0; i < options.Includes.Length; i++)
            {
                matcher.AddInclude(options.Includes[i]);
            }
        }

        for (var i = 0; i < options.Excludes.Length; i++)
        {
            matcher.AddExclude(options.Excludes[i]);
        }

        return matcher;
    }

    /// <summary>
    /// Recursively builds the <see cref="NavNode"/> for one directory.
    /// </summary>
    /// <param name="root">Absolute path to the input root (constant across recursion).</param>
    /// <param name="directory">Absolute path to the directory being built.</param>
    /// <param name="matcher">Pre-built glob matcher.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for prune diagnostics.</param>
    /// <returns>The section node for <paramref name="directory"/>, or null when a <c>.pages</c> override hides the section.</returns>
    private static NavNode? BuildSection(string root, string directory, Matcher matcher, in NavOptions options, ILogger logger)
    {
        var pagesOverride = PagesFileReader.ReadOrEmpty(Path.Combine(directory, PagesFileName));
        if (pagesOverride.Hide && directory != root)
        {
            var hiddenRelative = Path.GetRelativePath(root, directory).Replace('\\', '/');
            NavLoggingHelper.LogNavPruned(logger, hiddenRelative, ".pages hide:true");
            return null;
        }

        var files = Directory.GetFiles(directory, "*" + MarkdownExtension, SearchOption.TopDirectoryOnly);
        var subdirectories = Directory.GetDirectories(directory);

        var pageBuffer = ArrayPool<NavNode>.Shared.Rent(files.Length);
        var sectionBuffer = ArrayPool<NavNode>.Shared.Rent(subdirectories.Length);
        try
        {
            var pageCount = AppendPages(root, directory, files, matcher, pageBuffer, logger);
            var sectionCount = AppendSections(root, subdirectories, matcher, in options, sectionBuffer, logger);

            SortPages(pageBuffer, pageCount, in options);
            SortSections(sectionBuffer, sectionCount);

            var indexPath = string.Empty;
            if (options.Indexes && directory != root)
            {
                pageCount = ExtractIndexPage(pageBuffer, pageCount, out indexPath);
            }

            var children = MergeChildren(pageBuffer, pageCount, sectionBuffer, sectionCount);
            if (pagesOverride.OrderedEntries.Length > 0)
            {
                children = ApplyOrdering(children, pagesOverride.OrderedEntries);
            }

            var sectionTitle = directory == root ? string.Empty : Path.GetFileName(directory);
            if (!string.IsNullOrEmpty(pagesOverride.Title))
            {
                sectionTitle = pagesOverride.Title;
            }

            var sectionRelative = directory == root
                ? string.Empty
                : Path.GetRelativePath(root, directory).Replace('\\', '/');

            return new(sectionTitle, sectionRelative, isSection: true, children, indexPath);
        }
        finally
        {
            ArrayPool<NavNode>.Shared.Return(pageBuffer, clearArray: true);
            ArrayPool<NavNode>.Shared.Return(sectionBuffer, clearArray: true);
        }
    }

    /// <summary>Reorders <paramref name="children"/> to match the order in <paramref name="ordered"/>; entries not in <paramref name="ordered"/> follow in their original order.</summary>
    /// <param name="children">Built children.</param>
    /// <param name="ordered">Filenames or directory names from a <c>.pages</c> override.</param>
    /// <returns>A reordered child array.</returns>
    private static NavNode[] ApplyOrdering(NavNode[] children, string[] ordered)
    {
        var result = new NavNode[children.Length];
        var taken = new bool[children.Length];
        var write = 0;
        for (var i = 0; i < ordered.Length; i++)
        {
            var entry = ordered[i];
            for (var j = 0; j < children.Length; j++)
            {
                if (taken[j])
                {
                    continue;
                }

                if (!ChildMatches(children[j], entry))
                {
                    continue;
                }

                result[write++] = children[j];
                taken[j] = true;
                break;
            }
        }

        for (var j = 0; j < children.Length; j++)
        {
            if (!taken[j])
            {
                result[write++] = children[j];
            }
        }

        return result;
    }

    /// <summary>Determines whether <paramref name="node"/> is named by <paramref name="entry"/> (file name with or without <c>.md</c>, or section directory name).</summary>
    /// <param name="node">Candidate child.</param>
    /// <param name="entry">Entry text from <c>nav:</c>.</param>
    /// <returns>True on a match.</returns>
    private static bool ChildMatches(NavNode node, string entry)
    {
        if (node.IsSection)
        {
            var sectionName = node.RelativePath.AsSpan();
            var slash = sectionName.LastIndexOf('/');
            if (slash >= 0)
            {
                sectionName = sectionName[(slash + 1)..];
            }

            return sectionName.Equals(entry, StringComparison.OrdinalIgnoreCase);
        }

        var fileName = Path.GetFileName(node.RelativePath.AsSpan());
        if (fileName.Equals(entry, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var bare = Path.GetFileNameWithoutExtension(node.RelativePath.AsSpan());
        return bare.Equals(entry, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Appends matching markdown files as page nodes.</summary>
    /// <param name="root">Absolute input root.</param>
    /// <param name="directory">Directory being scanned.</param>
    /// <param name="files">Files in <paramref name="directory"/>.</param>
    /// <param name="matcher">Glob matcher.</param>
    /// <param name="buffer">Destination buffer; rented by the caller.</param>
    /// <param name="logger">Logger for prune diagnostics.</param>
    /// <returns>Number of pages written into <paramref name="buffer"/>.</returns>
    private static int AppendPages(string root, string directory, string[] files, Matcher matcher, NavNode[] buffer, ILogger logger)
    {
        var count = 0;
        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var match = matcher.Match(relative);
            if (!match.HasMatches)
            {
                NavLoggingHelper.LogNavPruned(logger, relative, "glob excluded");
                continue;
            }

            var flags = FrontmatterFlagReader.Read(file);
            if ((flags & PageFlags.NotInNav) != 0)
            {
                NavLoggingHelper.LogNavPruned(logger, relative, "frontmatter not_in_nav");
                continue;
            }

            var title = Path.GetFileNameWithoutExtension(file);
            buffer[count++] = new(title, relative, isSection: false, []);
            _ = directory;
        }

        return count;
    }

    /// <summary>Appends non-empty subdirectories as section nodes.</summary>
    /// <param name="root">Absolute input root.</param>
    /// <param name="subdirectories">Subdirectories of the current section.</param>
    /// <param name="matcher">Glob matcher.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="buffer">Destination buffer; rented by the caller.</param>
    /// <param name="logger">Logger for prune diagnostics.</param>
    /// <returns>Number of sections written into <paramref name="buffer"/>.</returns>
    private static int AppendSections(string root, string[] subdirectories, Matcher matcher, in NavOptions options, NavNode[] buffer, ILogger logger)
    {
        var count = 0;
        for (var i = 0; i < subdirectories.Length; i++)
        {
            var node = BuildSection(root, subdirectories[i], matcher, in options, logger);
            if (node is null)
            {
                continue;
            }

            if (options.HideEmptySections && node.Children.Length == 0)
            {
                NavLoggingHelper.LogNavPruned(logger, node.RelativePath, "empty section");
                continue;
            }

            buffer[count++] = node;
        }

        return count;
    }

    /// <summary>Removes the section's <c>index.md</c> (or <c>README.md</c>) page from <paramref name="buffer"/>, returning its source-relative path via <paramref name="indexPath"/>.</summary>
    /// <param name="buffer">Page buffer.</param>
    /// <param name="count">Current page count.</param>
    /// <param name="indexPath">Set to the index page's source-relative path on success; empty when no index was found.</param>
    /// <returns>The new page count.</returns>
    private static int ExtractIndexPage(NavNode[] buffer, int count, out string indexPath)
    {
        indexPath = string.Empty;
        for (var i = 0; i < count; i++)
        {
            var name = Path.GetFileNameWithoutExtension(buffer[i].RelativePath);
            if (!string.Equals(name, "index", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "README", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            indexPath = buffer[i].RelativePath;
            for (var j = i + 1; j < count; j++)
            {
                buffer[j - 1] = buffer[j];
            }

            return count - 1;
        }

        return count;
    }

    /// <summary>Sorts pages by the configured ordering rule.</summary>
    /// <param name="buffer">Page buffer.</param>
    /// <param name="count">Valid page count.</param>
    /// <param name="options">Plugin options.</param>
    private static void SortPages(NavNode[] buffer, int count, in NavOptions options)
    {
        switch (options.SortBy)
        {
            case NavSortBy.FileName:
            {
                Array.Sort(buffer, 0, count, NavNodeFileNameComparer.Instance);
                break;
            }

            case NavSortBy.Title:
            {
                Array.Sort(buffer, 0, count, NavNodeTitleComparer.Instance);
                break;
            }

            case NavSortBy.None:
            {
                break;
            }
        }
    }

    /// <summary>Sorts sections by directory name.</summary>
    /// <param name="buffer">Section buffer.</param>
    /// <param name="count">Valid section count.</param>
    private static void SortSections(NavNode[] buffer, int count) =>
        Array.Sort(buffer, 0, count, NavNodeFileNameComparer.Instance);

    /// <summary>Merges the page and section buffers into a single right-sized child array.</summary>
    /// <param name="pages">Page buffer.</param>
    /// <param name="pageCount">Valid page count.</param>
    /// <param name="sections">Section buffer.</param>
    /// <param name="sectionCount">Valid section count.</param>
    /// <returns>The merged children array.</returns>
    private static NavNode[] MergeChildren(NavNode[] pages, int pageCount, NavNode[] sections, int sectionCount)
    {
        var total = pageCount + sectionCount;
        if (total == 0)
        {
            return [];
        }

        var children = new NavNode[total];
        Array.Copy(pages, 0, children, 0, pageCount);
        Array.Copy(sections, 0, children, pageCount, sectionCount);
        return children;
    }
}
