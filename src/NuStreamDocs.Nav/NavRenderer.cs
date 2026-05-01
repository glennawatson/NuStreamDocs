// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Nav;

/// <summary>
/// Emits the nav tree as mkdocs-material-shaped HTML (<c>md-nav</c> /
/// <c>md-nav__list</c>), straight into a UTF-8 buffer.
/// </summary>
/// <remarks>
/// Two modes:
/// <list type="bullet">
/// <item><c>RenderFull</c> — every node, with the active branch flagged
/// via <c>md-nav__item--active</c>. Matches mkdocs-material's default.</item>
/// <item><c>RenderPruned</c> — only the ancestors of the current page
/// plus its siblings + immediate children. Matches mkdocs-material's
/// <c>navigation.prune</c>; on a 13K-page corpus this can drop per-page
/// HTML by an order of magnitude.</item>
/// </list>
/// Both paths walk the tree in one pass, write directly into the
/// supplied <see cref="IBufferWriter{T}"/>, and never allocate an
/// intermediate string. HTML attribute values come from controlled
/// inputs (URLs we built, titles from frontmatter or filenames), so
/// they're emitted without escaping; if the renderer ever takes
/// untrusted input, the attribute writer needs encoding added.
/// </remarks>
internal static class NavRenderer
{
    /// <summary>Length of the <c>.md</c> extension stripped when computing served URLs.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Emits the full nav tree for <paramref name="currentPageUrl"/>, marking the active branch.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <param name="currentPageUrl">URL of the page being rendered, forward-slashed (<c>guide/intro.html</c>).</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderFull(NavNode root, string currentPageUrl, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrEmpty(currentPageUrl);
        ArgumentNullException.ThrowIfNull(writer);

        var activeAncestors = CollectActiveAncestors(root, currentPageUrl);
        WriteList(writer, root.Children, currentPageUrl, activeAncestors, prune: false);
    }

    /// <summary>Emits the pruned nav for <paramref name="currentPageUrl"/>: only the active branch and its immediate context.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <param name="currentPageUrl">URL of the page being rendered.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderPruned(NavNode root, string currentPageUrl, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrEmpty(currentPageUrl);
        ArgumentNullException.ThrowIfNull(writer);

        var activeAncestors = CollectActiveAncestors(root, currentPageUrl);
        WriteList(writer, root.Children, currentPageUrl, activeAncestors, prune: true);
    }

    /// <summary>Returns the set of nodes that lie on the path from the root to the page matching <paramref name="currentPageUrl"/>.</summary>
    /// <param name="root">Nav root.</param>
    /// <param name="currentPageUrl">Active page URL.</param>
    /// <returns>Identity-set of active ancestors; empty when the page is not in the tree.</returns>
    private static HashSet<NavNode> CollectActiveAncestors(NavNode root, string currentPageUrl)
    {
        var set = new HashSet<NavNode>();
        FindActivePath(root, currentPageUrl, set);
        return set;
    }

    /// <summary>Depth-first search that records every ancestor of the active page in <paramref name="ancestors"/>.</summary>
    /// <param name="node">Current node.</param>
    /// <param name="currentPageUrl">Active page URL.</param>
    /// <param name="ancestors">Accumulator.</param>
    /// <returns>True when the active page sits in this subtree.</returns>
    private static bool FindActivePath(NavNode node, string currentPageUrl, HashSet<NavNode> ancestors)
    {
        if ((!node.IsSection && PathMatches(node.RelativePath, currentPageUrl)) ||
            (node.IsSection && node.IndexPath.Length > 0 && PathMatches(node.IndexPath, currentPageUrl)))
        {
            ancestors.Add(node);
            return true;
        }

        for (var i = 0; i < node.Children.Length; i++)
        {
            if (!FindActivePath(node.Children[i], currentPageUrl, ancestors))
            {
                continue;
            }

            ancestors.Add(node);
            return true;
        }

        return false;
    }

    /// <summary>True when a nav node's source path matches the rendered URL.</summary>
    /// <param name="navPath">Source-relative path stored on the node (e.g. <c>guide/intro.md</c>).</param>
    /// <param name="pageUrl">Site-relative URL (e.g. <c>guide/intro.html</c>).</param>
    /// <returns>True on match.</returns>
    private static bool PathMatches(string navPath, string pageUrl)
    {
        var asUrl = navPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? $"{navPath.AsSpan(0, navPath.Length - MarkdownExtensionLength)}.html"
            : navPath;
        return string.Equals(asUrl, pageUrl, StringComparison.Ordinal);
    }

    /// <summary>Writes one <c>&lt;ul class="md-nav__list"&gt;</c> with <paramref name="items"/> as <c>&lt;li&gt;</c>s.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="items">Child items to render.</param>
    /// <param name="currentPageUrl">Active page URL.</param>
    /// <param name="activeAncestors">Pre-computed active branch.</param>
    /// <param name="prune">When true, sub-lists collapse outside the active branch.</param>
    private static void WriteList(IBufferWriter<byte> writer, NavNode[] items, string currentPageUrl, HashSet<NavNode> activeAncestors, bool prune)
    {
        if (items.Length == 0)
        {
            return;
        }

        WriteUtf8(writer, "<ul class=\"md-nav__list\">"u8);
        for (var i = 0; i < items.Length; i++)
        {
            WriteItem(writer, items[i], currentPageUrl, activeAncestors, prune);
        }

        WriteUtf8(writer, "</ul>"u8);
    }

    /// <summary>Writes one <c>&lt;li&gt;</c> for either a section or a leaf page.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Node to render.</param>
    /// <param name="currentPageUrl">Active page URL.</param>
    /// <param name="activeAncestors">Pre-computed active branch.</param>
    /// <param name="prune">When true, sub-lists collapse outside the active branch.</param>
    private static void WriteItem(IBufferWriter<byte> writer, NavNode node, string currentPageUrl, HashSet<NavNode> activeAncestors, bool prune)
    {
        var active = activeAncestors.Contains(node);
        WriteUtf8(writer, active ? "<li class=\"md-nav__item md-nav__item--active\">"u8 : "<li class=\"md-nav__item\">"u8);

        if (node.IsSection)
        {
            WriteSection(writer, node, currentPageUrl, activeAncestors, prune, active);
        }
        else
        {
            WriteLeaf(writer, node, active);
        }

        WriteUtf8(writer, "</li>"u8);
    }

    /// <summary>Writes a section node's label + nested list.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Section node.</param>
    /// <param name="currentPageUrl">Active page URL.</param>
    /// <param name="activeAncestors">Active ancestors.</param>
    /// <param name="prune">When true, render children only when the section is on the active branch.</param>
    /// <param name="active">True when the section sits on the active branch.</param>
    private static void WriteSection(IBufferWriter<byte> writer, NavNode node, string currentPageUrl, HashSet<NavNode> activeAncestors, bool prune, bool active)
    {
        if (node.IndexPath.Length > 0)
        {
            WriteUtf8(writer, active ? "<a class=\"md-nav__link md-nav__link--active\" href=\""u8 : "<a class=\"md-nav__link\" href=\""u8);
            WriteString(writer, ToPageUrl(node.IndexPath));
            WriteUtf8(writer, "\">"u8);
            WriteString(writer, node.Title);
            WriteUtf8(writer, "</a>"u8);
        }
        else
        {
            WriteUtf8(writer, "<span class=\"md-nav__link\">"u8);
            WriteString(writer, node.Title);
            WriteUtf8(writer, "</span>"u8);
        }

        if (prune && !active)
        {
            // Pruned: skip children outside the active branch.
            return;
        }

        WriteList(writer, node.Children, currentPageUrl, activeAncestors, prune);
    }

    /// <summary>Writes a leaf node's anchor.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Leaf node.</param>
    /// <param name="active">True when this is the current page.</param>
    private static void WriteLeaf(IBufferWriter<byte> writer, NavNode node, bool active)
    {
        WriteUtf8(writer, active ? "<a class=\"md-nav__link md-nav__link--active\" href=\""u8 : "<a class=\"md-nav__link\" href=\""u8);
        WriteString(writer, ToPageUrl(node.RelativePath));
        WriteUtf8(writer, "\">"u8);
        WriteString(writer, node.Title);
        WriteUtf8(writer, "</a>"u8);
    }

    /// <summary>Translates a nav node's source-relative <c>.md</c> path to the served URL.</summary>
    /// <param name="navPath">Source-relative path.</param>
    /// <returns>Page-relative URL.</returns>
    private static string ToPageUrl(string navPath) =>
        navPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? $"{navPath.AsSpan(0, navPath.Length - MarkdownExtensionLength)}.html"
            : navPath;

    /// <summary>Bulk-writes UTF-8 bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void WriteUtf8(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>UTF-8-encodes <paramref name="value"/> directly into the writer's span.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="value">String.</param>
    private static void WriteString(IBufferWriter<byte> writer, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var dst = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length));
        var written = Encoding.UTF8.GetBytes(value, dst);
        writer.Advance(written);
    }
}
