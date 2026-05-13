// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Nav;

/// <summary>
/// Grafts <see cref="SyntheticNavEntry"/>s onto a nav tree built from the source folder. Synthetic
/// pages (generated API reference, blog index pages, etc.) never touch disk, so the disk walk
/// can't see them — a producer plugin instead surfaces lightweight metadata which this grafter
/// assembles into the matching section / page nodes and folds into the top-level children.
/// </summary>
internal static class SyntheticNavGrafter
{
    /// <summary>File name (case-insensitive) that promotes a synthetic page to its section's landing page.</summary>
    private const string IndexFileName = "index.md";

    /// <summary>Returns <paramref name="root"/> with the synthetic entries grafted in, or the same instance when there's nothing to add.</summary>
    /// <param name="root">Nav root built from the source folder.</param>
    /// <param name="entries">Synthetic nav metadata gathered from the registered plugins.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>The grafted root (a new node when entries changed it; otherwise <paramref name="root"/>).</returns>
    public static NavNode Graft(NavNode root, IReadOnlyList<SyntheticNavEntry> entries, bool useDirectoryUrls)
    {
        if (entries.Count == 0)
        {
            return root;
        }

        SectionBuilder synthetic = new(string.Empty, string.Empty);
        var placedAny = false;
        for (var i = 0; i < entries.Count; i++)
        {
            placedAny |= Place(synthetic, entries[i]);
        }

        if (!placedAny || (synthetic.Sections.Count == 0 && synthetic.Pages.Count == 0))
        {
            return root;
        }

        var merged = BuildMergedChildren(root, synthetic, useDirectoryUrls, out var changed);
        if (!changed)
        {
            return root;
        }

        // Any grafted/merged section may carry an Order; re-sort the full set the same way
        // NavTreeBuilder.MergeChildren does once a child has an explicit order.
        Array.Sort(merged, NavNodeFileNameComparer.Instance);
        return new(root.Title, root.RelativePath, root.IsSection, merged, root.IndexPath, useDirectoryUrls);
    }

    /// <summary>Folds the synthetic working tree's top-level entries into a copy of <paramref name="root"/>'s children.</summary>
    /// <param name="root">Nav root built from the source folder.</param>
    /// <param name="synthetic">Working root accumulating synthetic sections/pages.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <param name="changed">Set true when at least one child was added or replaced.</param>
    /// <returns>The merged children array (a fresh array even when unchanged).</returns>
    private static NavNode[] BuildMergedChildren(
        NavNode root,
        SectionBuilder synthetic,
        bool useDirectoryUrls,
        out bool changed)
    {
        List<NavNode> result = [.. root.Children];
        changed = false;

        foreach (var section in synthetic.Sections.Values)
        {
            changed |= AddOrMergeSection(result, root, section, useDirectoryUrls);
        }

        foreach (var page in synthetic.Pages)
        {
            if (!HasTopLevelChildNamed(root, StemOf(page.RelativePath)))
            {
                result.Add(ToPageNode(page, useDirectoryUrls));
                changed = true;
            }
        }

        return [.. result];
    }

    /// <summary>Adds or merges one synthetic top-level section into <paramref name="result"/>.</summary>
    /// <param name="result">Working child list (modified in place).</param>
    /// <param name="root">Nav root built from the source folder (collision check).</param>
    /// <param name="section">The synthetic working section.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>True when the list changed.</returns>
    private static bool AddOrMergeSection(
        List<NavNode> result,
        NavNode root,
        SectionBuilder section,
        bool useDirectoryUrls)
    {
        if (section.Hidden)
        {
            return false;
        }

        var existingIndex = IndexOfSectionNamed(result, section.Name);
        if (existingIndex >= 0)
        {
            // Section already on disk — fill in the synthetic index/title/order and any synthetic
            // sub-sections the disk walk didn't produce (e.g. a blog's tag archives).
            var merged = MergeIntoExistingSection(result[existingIndex], section, useDirectoryUrls);
            if (ReferenceEquals(merged, result[existingIndex]))
            {
                return false;
            }

            result[existingIndex] = merged;
            return true;
        }

        if (HasTopLevelChildNamed(root, section.Name))
        {
            // Collides with a disk *page* (not a section) — leave the disk content alone.
            return false;
        }

        if (ToNavNode(section, useDirectoryUrls) is not { } node)
        {
            return false;
        }

        result.Add(node);
        return true;
    }

    /// <summary>Returns a copy of <paramref name="diskSection"/> with the synthetic index/title/order and any new sub-sections/pages folded in, or the same instance when nothing changed.</summary>
    /// <param name="diskSection">The section node built from disk.</param>
    /// <param name="synthetic">The matching synthetic working section.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>The merged section node, or <paramref name="diskSection"/> when the merge is a no-op.</returns>
    private static NavNode MergeIntoExistingSection(
        NavNode diskSection,
        SectionBuilder synthetic,
        bool useDirectoryUrls)
    {
        var children = MergeSectionChildren(diskSection, synthetic, useDirectoryUrls);
        ResolveMergedMetadata(diskSection, synthetic, out var title, out var indexPath, out var order);

        if (ChildrenUnchanged(children, diskSection.Children)
            && ReferenceEquals(title, diskSection.Title)
            && indexPath.Value == diskSection.IndexPath.Value
            && order == diskSection.Order)
        {
            return diskSection;
        }

        return new(title, diskSection.RelativePath, true, children, indexPath, useDirectoryUrls) { Order = order };
    }

    /// <summary>Combines a disk section's children with the synthetic section: new sub-pages/sections are added; a synthetic page matching a disk page transfers its Order/title onto it.</summary>
    /// <param name="diskSection">The disk section node.</param>
    /// <param name="synthetic">The matching synthetic working section.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>The combined, sorted child array.</returns>
    private static NavNode[] MergeSectionChildren(NavNode diskSection, SectionBuilder synthetic, bool useDirectoryUrls)
    {
        List<NavNode> children = [.. diskSection.Children];
        foreach (var sub in synthetic.Sections.Values)
        {
            if (!HasChildNamed(diskSection.Children, sub.Name, true) && ToNavNode(sub, useDirectoryUrls) is { } node)
            {
                children.Add(node);
            }
        }

        foreach (var page in synthetic.Pages)
        {
            var existing = IndexOfPageNamed(children, StemOf(page.RelativePath));
            if (existing >= 0)
            {
                children[existing] = ApplySyntheticPageMetadata(children[existing], page, useDirectoryUrls);
            }
            else
            {
                children.Add(ToPageNode(page, useDirectoryUrls));
            }
        }

        var childArray = children.ToArray();
        Array.Sort(childArray, NavNodeFileNameComparer.Instance);
        return childArray;
    }

    /// <summary>True when <paramref name="a"/> and <paramref name="b"/> hold the same node instances in the same order.</summary>
    /// <param name="a">First array.</param>
    /// <param name="b">Second array.</param>
    /// <returns>True when unchanged.</returns>
    private static bool ChildrenUnchanged(NavNode[] a, NavNode[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Index of the page-typed child named <paramref name="name"/> in <paramref name="children"/>, or -1.</summary>
    /// <param name="children">Child list.</param>
    /// <param name="name">Page file stem.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOfPageNamed(List<NavNode> children, string name)
    {
        for (var i = 0; i < children.Count; i++)
        {
            if (!children[i].IsSection && NameOf(children[i]).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns <paramref name="diskPage"/> with the synthetic entry's <c>Order</c> (and title, when the entry carries one) applied, or the same instance when nothing changed.</summary>
    /// <param name="diskPage">The disk page node.</param>
    /// <param name="synthetic">The matching synthetic page entry.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>The page node with synthetic metadata applied.</returns>
    private static NavNode ApplySyntheticPageMetadata(NavNode diskPage, in PageEntry synthetic, bool useDirectoryUrls)
    {
        var title = synthetic.Title is { Length: > 0 } syntheticTitle ? syntheticTitle : diskPage.Title;
        var order = synthetic.Order ?? diskPage.Order;
        if (ReferenceEquals(title, diskPage.Title) && order == diskPage.Order)
        {
            return diskPage;
        }

        return new(title, diskPage.RelativePath, false, diskPage.Children, diskPage.IndexPath, useDirectoryUrls)
        {
            Order = order
        };
    }

    /// <summary>Picks the title/index/order for a merged section: a disk index page wins; otherwise the synthetic index fills the gaps.</summary>
    /// <param name="diskSection">The disk section node.</param>
    /// <param name="synthetic">The matching synthetic working section.</param>
    /// <param name="title">Resolved UTF-8 title.</param>
    /// <param name="indexPath">Resolved promoted-index path.</param>
    /// <param name="order">Resolved sort order.</param>
    private static void ResolveMergedMetadata(
        NavNode diskSection,
        SectionBuilder synthetic,
        out byte[] title,
        out FilePath indexPath,
        out int order)
    {
        title = diskSection.Title;
        indexPath = diskSection.IndexPath;
        order = diskSection.Order;
        if (!diskSection.IndexPath.IsEmpty)
        {
            // The disk section has its own index page — it already supplied the title/order.
            return;
        }

        if (synthetic.Title is { Length: > 0 } syntheticTitle)
        {
            title = syntheticTitle;
        }

        if (!string.IsNullOrEmpty(synthetic.IndexRelativePath))
        {
            indexPath = new(synthetic.IndexRelativePath);
        }

        if (order != int.MaxValue || synthetic.Order is not { } syntheticOrder)
        {
            return;
        }

        order = syntheticOrder;
    }

    /// <summary>Index of the section-typed child named <paramref name="name"/> in <paramref name="children"/>, or -1.</summary>
    /// <param name="children">Child list.</param>
    /// <param name="name">Section directory name.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOfSectionNamed(List<NavNode> children, string name)
    {
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].IsSection && NameOf(children[i]).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>True when <paramref name="children"/> has a child of the requested kind named <paramref name="name"/>.</summary>
    /// <param name="children">Child array.</param>
    /// <param name="name">Candidate name.</param>
    /// <param name="isSection">When true match section nodes, otherwise page nodes.</param>
    /// <returns>True on a match.</returns>
    private static bool HasChildNamed(NavNode[] children, string name, bool isSection)
    {
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i].IsSection == isSection &&
                NameOf(children[i]).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Routes <paramref name="entry"/> into the working tree under <paramref name="syntheticRoot"/>.</summary>
    /// <param name="syntheticRoot">Working root accumulating synthetic sections/pages.</param>
    /// <param name="entry">The entry to place.</param>
    /// <returns>True when the entry contributed a node (i.e. wasn't malformed or hidden away).</returns>
    private static bool Place(SectionBuilder syntheticRoot, in SyntheticNavEntry entry)
    {
        var rel = entry.RelativePath.Value;
        if (string.IsNullOrEmpty(rel))
        {
            return false;
        }

        var segments = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var section = syntheticRoot;
        var accumulated = string.Empty;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            accumulated = accumulated.Length == 0 ? segments[i] : accumulated + "/" + segments[i];
            if (!section.Sections.TryGetValue(segments[i], out var child))
            {
                child = new(segments[i], accumulated);
                section.Sections[segments[i]] = child;
            }

            section = child;
        }

        var fileName = segments[^1];
        if (fileName.Equals(IndexFileName, StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
        {
            section.IndexRelativePath = rel;
            section.Title = entry.Title;
            section.Order = entry.Order;
            section.Hidden = entry.Hidden;
            return true;
        }

        if (entry.Hidden)
        {
            return false;
        }

        section.Pages.Add(new(rel, entry.Title, entry.Order));
        return true;
    }

    /// <summary>Converts a working section to a <see cref="NavNode"/>, or null when hidden / empty.</summary>
    /// <param name="section">Working section.</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>The section node, or null.</returns>
    private static NavNode? ToNavNode(SectionBuilder section, bool useDirectoryUrls)
    {
        if (section.Hidden)
        {
            return null;
        }

        var children = new List<NavNode>(section.Sections.Count + section.Pages.Count);
        foreach (var child in section.Sections.Values)
        {
            if (ToNavNode(child, useDirectoryUrls) is { } node)
            {
                children.Add(node);
            }
        }

        foreach (var page in section.Pages)
        {
            children.Add(ToPageNode(page, useDirectoryUrls));
        }

        // Drop a synthetic section that ended up with no index page and no children — nothing to link to.
        if (children.Count == 0 && string.IsNullOrEmpty(section.IndexRelativePath))
        {
            return null;
        }

        var childArray = children.ToArray();
        Array.Sort(childArray, NavNodeFileNameComparer.Instance);

        var title = section.Title is { Length: > 0 } t ? t : Encoding.UTF8.GetBytes(section.Name);
        return new(
            title,
            new(section.RelativePath),
            true,
            childArray,
            string.IsNullOrEmpty(section.IndexRelativePath) ? default : new FilePath(section.IndexRelativePath),
            useDirectoryUrls)
        { Order = section.Order ?? int.MaxValue };
    }

    /// <summary>Converts a working page to a leaf <see cref="NavNode"/>.</summary>
    /// <param name="page">Working page (relative path + optional title/order).</param>
    /// <param name="useDirectoryUrls">True when the rendered site uses directory-style URLs.</param>
    /// <returns>The leaf node.</returns>
    private static NavNode ToPageNode(in PageEntry page, bool useDirectoryUrls)
    {
        var title = page.Title is { Length: > 0 } t ? t : Encoding.UTF8.GetBytes(StemOf(page.RelativePath));
        return new(title, new(page.RelativePath), false, [], useDirectoryUrls) { Order = page.Order ?? int.MaxValue };
    }

    /// <summary>True when <paramref name="root"/> already has a top-level child whose name (section dir or page stem) matches <paramref name="name"/>.</summary>
    /// <param name="root">Nav root.</param>
    /// <param name="name">Candidate name.</param>
    /// <returns>True on a collision.</returns>
    private static bool HasTopLevelChildNamed(NavNode root, string name)
    {
        for (var i = 0; i < root.Children.Length; i++)
        {
            if (NameOf(root.Children[i]).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the section directory name or the page file stem for <paramref name="node"/>.</summary>
    /// <param name="node">Nav node.</param>
    /// <returns>The bare name.</returns>
    private static string NameOf(NavNode node)
    {
        var rel = node.RelativePath.Value;
        var slash = rel.LastIndexOf('/');
        var name = slash >= 0 ? rel[(slash + 1)..] : rel;
        return node.IsSection ? name : StemOf(name);
    }

    /// <summary>Strips the directory and <c>.md</c> extension from <paramref name="relativeOrFileName"/>.</summary>
    /// <param name="relativeOrFileName">A relative path or bare file name.</param>
    /// <returns>The file stem.</returns>
    private static string StemOf(string relativeOrFileName)
    {
        var slash = relativeOrFileName.LastIndexOf('/');
        var name = slash >= 0 ? relativeOrFileName[(slash + 1)..] : relativeOrFileName;
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    /// <summary>Working leaf-page record while a synthetic section is being assembled.</summary>
    /// <param name="RelativePath">Forward-slashed path relative to the input root.</param>
    /// <param name="Title">UTF-8 title, or null to fall back to the file stem.</param>
    /// <param name="Order">Sort order, or null for unordered.</param>
    private readonly record struct PageEntry(string RelativePath, byte[]? Title, int? Order);

    /// <summary>Working tree node for a synthetic section while it's being assembled.</summary>
    private sealed class SectionBuilder(string name, string relativePath)
    {
        /// <summary>Gets the last path segment (directory name).</summary>
        public string Name { get; } = name;

        /// <summary>Gets the forward-slashed path of this section relative to the input root.</summary>
        public string RelativePath { get; } = relativePath;

        /// <summary>Gets or sets the relative path of the promoted <c>index.md</c>, or null when the section has none.</summary>
        public string? IndexRelativePath { get; set; }

        /// <summary>Gets or sets the UTF-8 section title (from its <c>index.md</c>), or null to fall back to <see cref="Name"/>.</summary>
        public byte[]? Title { get; set; }

        /// <summary>Gets or sets the section sort order (from its <c>index.md</c>), or null for unordered.</summary>
        public int? Order { get; set; }

        /// <summary>Gets or sets a value indicating whether the section is hidden from the nav.</summary>
        public bool Hidden { get; set; }

        /// <summary>Gets the child sections keyed by directory name (case-insensitive).</summary>
        public Dictionary<string, SectionBuilder> Sections { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the leaf pages directly under this section.</summary>
        public List<PageEntry> Pages { get; } = [];
    }
}
