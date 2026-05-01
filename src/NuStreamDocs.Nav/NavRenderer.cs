// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

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
    /// <summary>Emits the full nav tree for <paramref name="currentPageUrl"/>, marking the active branch.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <param name="currentPageUrl">URL of the page being rendered, forward-slashed (<c>guide/intro.html</c>).</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderFull(NavNode root, string currentPageUrl, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrEmpty(currentPageUrl);
        ArgumentNullException.ThrowIfNull(writer);

        EnsureParentsAttached(root);
        var activeNode = FindActiveNode(root, currentPageUrl);
        var activeBranch = BuildActiveBranchSet(activeNode);
        WriteList(writer, root.Children, activeBranch, prune: false);
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

        EnsureParentsAttached(root);
        var activeNode = FindActiveNode(root, currentPageUrl);
        var activeBranch = BuildActiveBranchSet(activeNode);
        WriteList(writer, root.Children, activeBranch, prune: true);
    }

    /// <summary>Materialises the active-ancestor chain into a small reference-equality set.</summary>
    /// <param name="activeNode">Active node, or null when no page is active.</param>
    /// <returns>Set of nodes on the active branch — empty when there is no active node.</returns>
    /// <remarks>
    /// Built once per page. Without it, every visited node would walk
    /// its parent chain at render time — O(N·D) per page on the full
    /// tree. With it, the per-node check collapses to a hash lookup.
    /// Sized to the active depth (≤ ~8 on real corpora) so it stays
    /// pool-friendly even when called for every page in a large build.
    /// </remarks>
    private static HashSet<NavNode> BuildActiveBranchSet(NavNode? activeNode)
    {
        if (activeNode is null)
        {
            return [];
        }

        // Pre-size for the chain depth; reference-equality so each
        // ancestor is distinct without paying string-hash costs.
        var set = new HashSet<NavNode>(8, ReferenceEqualityComparer.Instance);
        for (var current = activeNode; current is not null; current = current.Parent)
        {
            set.Add(current);
        }

        return set;
    }

    /// <summary>Attaches parent links when the tree was built manually in tests rather than via <see cref="NavTreeBuilder"/>.</summary>
    /// <param name="root">Nav root.</param>
    private static void EnsureParentsAttached(NavNode root)
    {
        if (root.Children is not [var firstChild, ..] || ReferenceEquals(firstChild.Parent, root))
        {
            return;
        }

        root.AttachParents();
    }

    /// <summary>Returns the node representing <paramref name="currentPageUrl"/>, or null when the page is not in the tree.</summary>
    /// <param name="root">Nav root.</param>
    /// <param name="currentPageUrl">Active page URL.</param>
    /// <returns>The active node, or null when not found.</returns>
    private static NavNode? FindActiveNode(NavNode root, string currentPageUrl)
    {
        if ((!root.IsSection && string.Equals(root.RelativeUrl, currentPageUrl, StringComparison.Ordinal)) ||
            (root.IsSection && root.IndexUrl.Length > 0 && string.Equals(root.IndexUrl, currentPageUrl, StringComparison.Ordinal)))
        {
            return root;
        }

        for (var i = 0; i < root.Children.Length; i++)
        {
            var active = FindActiveNode(root.Children[i], currentPageUrl);
            if (active is not null)
            {
                return active;
            }
        }

        return null;
    }

    /// <summary>Writes one <c>&lt;ul class="md-nav__list"&gt;</c> with <paramref name="items"/> as <c>&lt;li&gt;</c>s.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="items">Child items to render.</param>
    /// <param name="activeBranch">Reference-equality set of nodes on the active branch (empty when there is no active page).</param>
    /// <param name="prune">When true, sub-lists collapse outside the active branch.</param>
    private static void WriteList(IBufferWriter<byte> writer, NavNode[] items, HashSet<NavNode> activeBranch, bool prune)
    {
        if (items.Length == 0)
        {
            return;
        }

        WriteUtf8(writer, "<ul class=\"md-nav__list\">"u8);
        for (var i = 0; i < items.Length; i++)
        {
            WriteItem(writer, items[i], activeBranch, prune);
        }

        WriteUtf8(writer, "</ul>"u8);
    }

    /// <summary>Writes one <c>&lt;li&gt;</c> for either a section or a leaf page.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Node to render.</param>
    /// <param name="activeBranch">Reference-equality set of nodes on the active branch.</param>
    /// <param name="prune">When true, sub-lists collapse outside the active branch.</param>
    private static void WriteItem(IBufferWriter<byte> writer, NavNode node, HashSet<NavNode> activeBranch, bool prune)
    {
        var active = activeBranch.Contains(node);
        WriteUtf8(writer, active ? "<li class=\"md-nav__item md-nav__item--active\">"u8 : "<li class=\"md-nav__item\">"u8);

        if (node.IsSection)
        {
            WriteSection(writer, node, activeBranch, prune, active);
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
    /// <param name="activeBranch">Reference-equality set of nodes on the active branch.</param>
    /// <param name="prune">When true, render children only when the section is on the active branch.</param>
    /// <param name="active">True when the section sits on the active branch.</param>
    private static void WriteSection(IBufferWriter<byte> writer, NavNode node, HashSet<NavNode> activeBranch, bool prune, bool active)
    {
        if (node.IndexPath.Length > 0)
        {
            WriteUtf8(writer, active ? "<a class=\"md-nav__link md-nav__link--active\" href=\""u8 : "<a class=\"md-nav__link\" href=\""u8);
            WriteString(writer, node.IndexUrl);
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

        WriteList(writer, node.Children, activeBranch, prune);
    }

    /// <summary>Writes a leaf node's anchor.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Leaf node.</param>
    /// <param name="active">True when this is the current page.</param>
    private static void WriteLeaf(IBufferWriter<byte> writer, NavNode node, bool active)
    {
        WriteUtf8(writer, active ? "<a class=\"md-nav__link md-nav__link--active\" href=\""u8 : "<a class=\"md-nav__link\" href=\""u8);
        WriteString(writer, node.RelativeUrl);
        WriteUtf8(writer, "\">"u8);
        WriteString(writer, node.Title);
        WriteUtf8(writer, "</a>"u8);
    }

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
    private static void WriteString(IBufferWriter<byte> writer, string value) => Utf8StringWriter.Write(writer, value);
}
