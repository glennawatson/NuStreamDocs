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
    /// <summary>Emits the full nav tree, marking <paramref name="activeNode"/>'s branch.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <param name="activeNode">
    /// Pre-resolved active node, or null when no page is active. The
    /// caller resolves the URL via an O(1) index built once per build,
    /// so per-page rendering doesn't walk the whole tree.
    /// </param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderFull(NavNode root, NavNode? activeNode, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(writer);

        EnsureParentsAttached(root);
        var activeBranch = BuildActiveBranchSet(activeNode);
        WriteList(writer, root.Children, activeBranch, prune: false);
    }

    /// <summary>Emits the pruned nav: only the active branch and its immediate context.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <param name="activeNode">Pre-resolved active node, or null when no page is active.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderPruned(NavNode root, NavNode? activeNode, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(writer);

        EnsureParentsAttached(root);
        var activeBranch = BuildActiveBranchSet(activeNode);
        WriteList(writer, root.Children, activeBranch, prune: true);
    }

    /// <summary>Emits a horizontal tab bar from the root's top-level children (mkdocs-material's <c>navigation.tabs</c>).</summary>
    /// <param name="root">Nav tree root.</param>
    /// <param name="activeNode">Pre-resolved active node; the tab whose subtree contains it receives the <c>md-tabs__item--active</c> class.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderTabs(NavNode root, NavNode? activeNode, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(writer);

        EnsureParentsAttached(root);
        var activeBranch = BuildActiveBranchSet(activeNode);

        WriteUtf8(writer, "<nav class=\"md-tabs\" aria-label=\"Tabs\" data-md-component=\"tabs\"><div class=\"md-tabs__inner md-grid\"><ul class=\"md-tabs__list\">"u8);
        for (var i = 0; i < root.Children.Length; i++)
        {
            WriteTabItem(writer, root.Children[i], activeBranch);
        }

        WriteUtf8(writer, "</ul></div></nav>"u8);
    }

    /// <summary>Indexes every node in the tree by UTF-8 URL bytes so per-page rendering can resolve the active node in O(1) without re-encoding the lookup key.</summary>
    /// <remarks>Indexes section <see cref="NavNode.IndexUrlBytes"/> and leaf <see cref="NavNode.RelativeUrlBytes"/> entries.</remarks>
    /// <param name="root">Nav tree root.</param>
    /// <returns>UTF-8 URL → node map sized to the visited node count.</returns>
    public static Dictionary<byte[], NavNode> BuildUrlIndex(NavNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var index = new Dictionary<byte[], NavNode>(ByteArrayComparer.Instance);
        IndexNode(root, index);
        return index;
    }

    /// <summary>Recursive helper for <see cref="BuildUrlIndex"/>.</summary>
    /// <param name="node">Current node.</param>
    /// <param name="index">Accumulator.</param>
    private static void IndexNode(NavNode node, Dictionary<byte[], NavNode> index)
    {
        if (!node.IsSection && node.RelativeUrlBytes.Length > 0)
        {
            index[node.RelativeUrlBytes] = node;
        }

        if (node.IsSection && node.IndexUrlBytes.Length > 0)
        {
            index[node.IndexUrlBytes] = node;
        }

        for (var i = 0; i < node.Children.Length; i++)
        {
            IndexNode(node.Children[i], index);
        }
    }

    /// <summary>Materializes the active-ancestor chain into a small reference-equality set.</summary>
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
        WriteUtf8(writer, ResolveItemOpenTag(node, active));

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

    /// <summary>Returns the right opening <c>&lt;li&gt;</c> tag for <paramref name="node"/>, with <c>--nested</c> / <c>--active</c> modifiers as appropriate.</summary>
    /// <param name="node">Node being rendered.</param>
    /// <param name="active">True when the node sits on the active branch.</param>
    /// <returns>UTF-8 bytes for the opening tag.</returns>
    private static ReadOnlySpan<byte> ResolveItemOpenTag(NavNode node, bool active) => (node.IsSection, active) switch
    {
        (true, true) => "<li class=\"md-nav__item md-nav__item--nested md-nav__item--active\">"u8,
        (true, false) => "<li class=\"md-nav__item md-nav__item--nested\">"u8,
        (false, true) => "<li class=\"md-nav__item md-nav__item--active\">"u8,
        (false, false) => "<li class=\"md-nav__item\">"u8,
    };

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
            WriteUtf8(writer, node.IndexUrlBytes);
            WriteUtf8(writer, "\">"u8);
            WriteUtf8(writer, node.TitleBytes);
            WriteUtf8(writer, "</a>"u8);
        }
        else
        {
            WriteUtf8(writer, "<span class=\"md-nav__link\">"u8);
            WriteUtf8(writer, node.TitleBytes);
            WriteUtf8(writer, "</span>"u8);
        }

        if (prune && !active)
        {
            // Pruned: skip children outside the active branch.
            return;
        }

        // Wrap the child list in <nav class="md-nav"> + <label class="md-nav__title"> so mkdocs-material's CSS
        // recognises a nested section (folding chevron, indent, label).
        WriteUtf8(writer, "<nav class=\"md-nav\" aria-label=\""u8);
        WriteUtf8(writer, node.TitleBytes);
        WriteUtf8(writer, "\"><label class=\"md-nav__title\">"u8);
        WriteUtf8(writer, node.TitleBytes);
        WriteUtf8(writer, "</label>"u8);
        WriteList(writer, node.Children, activeBranch, prune);
        WriteUtf8(writer, "</nav>"u8);
    }

    /// <summary>Writes a leaf node's anchor.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Leaf node.</param>
    /// <param name="active">True when this is the current page.</param>
    private static void WriteLeaf(IBufferWriter<byte> writer, NavNode node, bool active)
    {
        WriteUtf8(writer, active ? "<a class=\"md-nav__link md-nav__link--active\" href=\""u8 : "<a class=\"md-nav__link\" href=\""u8);
        WriteUtf8(writer, node.RelativeUrlBytes);
        WriteUtf8(writer, "\">"u8);
        WriteUtf8(writer, node.TitleBytes);
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

    /// <summary>Emits a single <c>&lt;li class="md-tabs__item"&gt;</c> for <paramref name="node"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Top-level nav node.</param>
    /// <param name="activeBranch">Active-branch set; the tab is flagged active when its subtree contains the active node.</param>
    private static void WriteTabItem(IBufferWriter<byte> writer, NavNode node, HashSet<NavNode> activeBranch)
    {
        var active = activeBranch.Contains(node);
        WriteUtf8(writer, active ? "<li class=\"md-tabs__item md-tabs__item--active\">"u8 : "<li class=\"md-tabs__item\">"u8);

        var href = TabHref(node);
        if (href.Length is 0)
        {
            WriteUtf8(writer, "<span class=\"md-tabs__link\">"u8);
            WriteUtf8(writer, node.TitleBytes);
            WriteUtf8(writer, "</span>"u8);
        }
        else
        {
            WriteUtf8(writer, "<a class=\"md-tabs__link\" href=\""u8);
            WriteUtf8(writer, href);
            WriteUtf8(writer, "\">"u8);
            WriteUtf8(writer, node.TitleBytes);
            WriteUtf8(writer, "</a>"u8);
        }

        WriteUtf8(writer, "</li>"u8);
    }

    /// <summary>Picks the URL the tab links to: the section's index page when present, the leaf URL otherwise.</summary>
    /// <param name="node">Top-level nav node.</param>
    /// <returns>UTF-8 URL bytes; empty when no link is available.</returns>
    private static ReadOnlySpan<byte> TabHref(NavNode node) => node.IsSection ? node.IndexUrlBytes : node.RelativeUrlBytes;
}
