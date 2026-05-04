// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
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
        WriteList(writer, root.Children, activeBranch, prune: false, level: 0);
    }

    /// <summary>Emits the primary sidebar tree, scoping to the active top-level section when one is selected.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <param name="activeNode">Pre-resolved active node, or null when no page is active.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderSidebarFull(NavNode root, NavNode? activeNode, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(writer);

        EnsureParentsAttached(root);
        var activeBranch = BuildActiveBranchSet(activeNode);
        WriteList(writer, ResolveSidebarItems(root, activeBranch), activeBranch, prune: false, level: 0);
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
        WriteList(writer, root.Children, activeBranch, prune: true, level: 0);
    }

    /// <summary>Emits the pruned primary sidebar tree, scoping to the active top-level section when one is selected.</summary>
    /// <param name="root">Nav tree root.</param>
    /// <param name="activeNode">Pre-resolved active node, or null when no page is active.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderSidebarPruned(NavNode root, NavNode? activeNode, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(writer);

        EnsureParentsAttached(root);
        var activeBranch = BuildActiveBranchSet(activeNode);
        WriteList(writer, ResolveSidebarItems(root, activeBranch), activeBranch, prune: true, level: 0);
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
            var child = root.Children[i];
            if (IsTopLevelHomePage(child))
            {
                // The home page is reachable via the brand link, mkdocs-material does the same.
                continue;
            }

            WriteTabItem(writer, child, activeBranch);
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

    /// <summary>Returns true when <paramref name="node"/> is a top-level <c>index.md</c> / <c>README.md</c> leaf that should not be emitted as its own tab.</summary>
    /// <param name="node">Top-level child of the nav root.</param>
    /// <returns>True when the node is the implicit home page; false otherwise.</returns>
    private static bool IsTopLevelHomePage(NavNode node)
    {
        if (node.IsSection)
        {
            return false;
        }

        var relative = node.RelativePath.Value;
        if (string.IsNullOrEmpty(relative))
        {
            return false;
        }

        // Top-level only — nested home pages live inside their section and never reach the tab strip.
        if (relative.AsSpan().IndexOfAny(['/', '\\']) >= 0)
        {
            return false;
        }

        return string.Equals(relative, "index.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(relative, "README.md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns the scoped primary-sidebar items for the active page.</summary>
    /// <param name="root">Nav root.</param>
    /// <param name="activeBranch">Nodes on the active branch.</param>
    /// <returns>The items the primary sidebar should render.</returns>
    private static NavNode[] ResolveSidebarItems(NavNode root, HashSet<NavNode> activeBranch)
    {
        NavNode? home = null;
        NavNode? activeSection = null;
        for (var i = 0; i < root.Children.Length; i++)
        {
            var child = root.Children[i];
            if (home is null && IsTopLevelHomePage(child))
            {
                home = child;
                continue;
            }

            if (activeSection is null && child.IsSection && activeBranch.Contains(child))
            {
                activeSection = child;
            }
        }

        if (activeSection is null)
        {
            return root.Children;
        }

        return ComposeSidebarItems(home, activeSection);
    }

    /// <summary>Builds the final contextual sidebar item array from the optional home leaf and active section.</summary>
    /// <param name="home">Top-level home leaf, when present.</param>
    /// <param name="activeSection">Active top-level section.</param>
    /// <returns>The contextual sidebar item array.</returns>
    /// <remarks>
    /// When the active section's only child is a single leaf page, the children are hoisted up so
    /// the sidebar doesn't render the redundant section wrapper around a one-page subtree. For any
    /// other shape (multiple children, or children that are themselves sections) the active
    /// section is kept as the wrapper so readers see the expandable Material drawer header
    /// (<c>--active --section --nested</c>) and have a sense of place when scrolling 100+ peers.
    /// </remarks>
    private static NavNode[] ComposeSidebarItems(NavNode? home, NavNode activeSection)
    {
        if (ShouldHoistSingleLeaf(activeSection))
        {
            return home is null ? activeSection.Children : ComposeWithHomePrepended(home, activeSection.Children);
        }

        return home is null ? [activeSection] : [home, activeSection];
    }

    /// <summary>True when the active section is a single-leaf wrapper whose children should be hoisted in place of the section header.</summary>
    /// <param name="activeSection">Active top-level section.</param>
    /// <returns>True only when the section contains exactly one child and that child is a leaf page.</returns>
    private static bool ShouldHoistSingleLeaf(NavNode activeSection) =>
        activeSection.Children is [{ IsSection: false }];

    /// <summary>Prepends <paramref name="home"/> onto <paramref name="children"/> as a fresh array.</summary>
    /// <param name="home">Top-level home leaf.</param>
    /// <param name="children">Hoisted children.</param>
    /// <returns>A right-sized array starting with home.</returns>
    private static NavNode[] ComposeWithHomePrepended(NavNode home, NavNode[] children)
    {
        var items = new NavNode[children.Length + 1];
        items[0] = home;
        Array.Copy(children, 0, items, 1, children.Length);
        return items;
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
    /// <param name="level">Current nav depth.</param>
    private static void WriteList(IBufferWriter<byte> writer, NavNode[] items, HashSet<NavNode> activeBranch, bool prune, int level)
    {
        if (items.Length == 0)
        {
            return;
        }

        WriteUtf8(writer, "<ul class=\"md-nav__list\" data-md-scrollfix>"u8);
        for (var i = 0; i < items.Length; i++)
        {
            WriteItem(writer, items[i], activeBranch, prune, level);
        }

        WriteUtf8(writer, "</ul>"u8);
    }

    /// <summary>Writes one <c>&lt;li&gt;</c> for either a section or a leaf page.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Node to render.</param>
    /// <param name="activeBranch">Reference-equality set of nodes on the active branch.</param>
    /// <param name="prune">When true, sub-lists collapse outside the active branch.</param>
    /// <param name="level">Current nav depth.</param>
    private static void WriteItem(IBufferWriter<byte> writer, NavNode node, HashSet<NavNode> activeBranch, bool prune, int level)
    {
        var active = activeBranch.Contains(node);
        WriteUtf8(writer, ResolveItemOpenTag(node, active, prune));

        if (node.IsSection)
        {
            WriteSection(writer, node, activeBranch, prune, active, level);
        }
        else
        {
            WriteLeaf(writer, node, active);
        }

        WriteUtf8(writer, "</li>"u8);
    }

    /// <summary>Returns the right opening <c>&lt;li&gt;</c> tag for <paramref name="node"/>.</summary>
    /// <param name="node">Node being rendered.</param>
    /// <param name="active">True when the node sits on the active branch.</param>
    /// <param name="prune">True when the tree is being rendered in prune mode.</param>
    /// <returns>UTF-8 bytes for the opening tag.</returns>
    private static ReadOnlySpan<byte> ResolveItemOpenTag(NavNode node, bool active, bool prune)
    {
        if (node.IsSection)
        {
            if (active)
            {
                return "<li class=\"md-nav__item md-nav__item--active md-nav__item--section md-nav__item--nested\">"u8;
            }

            return prune
                ? "<li class=\"md-nav__item md-nav__item--pruned md-nav__item--nested\">"u8
                : "<li class=\"md-nav__item md-nav__item--nested\">"u8;
        }

        if (active)
        {
            return "<li class=\"md-nav__item md-nav__item--active\">"u8;
        }

        return prune
            ? "<li class=\"md-nav__item md-nav__item--pruned\">"u8
            : "<li class=\"md-nav__item\">"u8;
    }

    /// <summary>Writes a section node's label + nested list.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Section node.</param>
    /// <param name="activeBranch">Reference-equality set of nodes on the active branch.</param>
    /// <param name="prune">When true, render children only when the section is on the active branch.</param>
    /// <param name="active">True when the section sits on the active branch.</param>
    /// <param name="level">Current nav depth.</param>
    private static void WriteSection(IBufferWriter<byte> writer, NavNode node, HashSet<NavNode> activeBranch, bool prune, bool active, int level)
    {
        var hasHref = TryGetSidebarHref(node, out var href, out var appendTrailingSlash);
        if (hasHref)
        {
            WriteUtf8(writer, active ? "<a class=\"md-nav__link md-nav__link--active\" href=\""u8 : "<a class=\"md-nav__link\" href=\""u8);
            WriteRootRelativeHref(writer, href, appendTrailingSlash);
            WriteUtf8(writer, "\">"u8);
        }
        else
        {
            WriteUtf8(writer, active ? "<span class=\"md-nav__link md-nav__link--active\">"u8 : "<span class=\"md-nav__link\">"u8);
        }

        WriteTitleSpan(writer, node.Title);
        if (prune && !active && node.Children is [_, ..])
        {
            WriteUtf8(writer, "<span class=\"md-nav__icon md-icon\"></span>"u8);
        }

        WriteUtf8(writer, hasHref ? "</a>"u8 : "</span>"u8);

        if (prune && !active)
        {
            return;
        }

        WriteUtf8(writer, "<nav class=\"md-nav\" data-md-level=\""u8);
        WriteLevel(writer, level + 1);
        WriteUtf8(writer, "\" aria-label=\""u8);
        WriteUtf8(writer, node.Title);
        WriteUtf8(writer, "\">"u8);
        WriteList(writer, node.Children, activeBranch, prune, level + 1);
        WriteUtf8(writer, "</nav>"u8);
    }

    /// <summary>Writes a leaf node's anchor.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="node">Leaf node.</param>
    /// <param name="active">True when this is the current page.</param>
    private static void WriteLeaf(IBufferWriter<byte> writer, NavNode node, bool active)
    {
        WriteUtf8(writer, active ? "<a class=\"md-nav__link md-nav__link--active\" href=\""u8 : "<a class=\"md-nav__link\" href=\""u8);
        WriteRootRelativeHref(writer, node.RelativeUrlBytes, appendTrailingSlash: false);
        WriteUtf8(writer, "\">"u8);
        WriteTitleSpan(writer, IsTopLevelHomePage(node) ? "Home"u8 : node.Title);
        WriteUtf8(writer, "</a>"u8);
    }

    /// <summary>Writes the title body used inside nav links.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="title">Title bytes.</param>
    private static void WriteTitleSpan(IBufferWriter<byte> writer, ReadOnlySpan<byte> title)
    {
        WriteUtf8(writer, "<span class=\"md-ellipsis\">"u8);
        WriteUtf8(writer, title);
        WriteUtf8(writer, "</span>"u8);
    }

    /// <summary>Writes a small decimal integer into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="value">Value to write.</param>
    private static void WriteLevel(IBufferWriter<byte> writer, int value)
    {
        Span<byte> digits = stackalloc byte[11];
        if (!Utf8Formatter.TryFormat(value, digits, out var written))
        {
            return;
        }

        WriteUtf8(writer, digits[..written]);
    }

    /// <summary>Returns the sidebar href for a section: index page, first leaf descendant, or section root.</summary>
    /// <param name="node">Section node.</param>
    /// <param name="href">Resolved href bytes without a leading slash.</param>
    /// <param name="appendTrailingSlash">True when the href should end in a slash.</param>
    /// <returns>True when a link is available.</returns>
    private static bool TryGetSidebarHref(NavNode node, out ReadOnlySpan<byte> href, out bool appendTrailingSlash)
    {
        if (node.IndexUrlBytes.Length > 0)
        {
            href = node.IndexUrlBytes;
            appendTrailingSlash = false;
            return true;
        }

        if (TryGetFirstLeafHref(node, out href))
        {
            appendTrailingSlash = false;
            return true;
        }

        if (node.RelativeUrlBytes.Length > 0)
        {
            href = node.RelativeUrlBytes;
            appendTrailingSlash = true;
            return true;
        }

        href = default;
        appendTrailingSlash = false;
        return false;
    }

    /// <summary>Returns the first descendant leaf href for <paramref name="node"/>.</summary>
    /// <param name="node">Section node.</param>
    /// <param name="href">Resolved href bytes without a leading slash.</param>
    /// <returns>True when a descendant leaf exists.</returns>
    private static bool TryGetFirstLeafHref(NavNode node, out ReadOnlySpan<byte> href)
    {
        for (var i = 0; i < node.Children.Length; i++)
        {
            var child = node.Children[i];
            if (!child.IsSection)
            {
                href = child.RelativeUrlBytes;
                return true;
            }

            if (child.IndexUrlBytes.Length > 0)
            {
                href = child.IndexUrlBytes;
                return true;
            }

            if (TryGetFirstLeafHref(child, out href))
            {
                return true;
            }
        }

        href = default;
        return false;
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

        if (!TryGetTabHref(node, out var href, out var appendTrailingSlash))
        {
            WriteUtf8(writer, "<span class=\"md-tabs__link\">"u8);
            WriteUtf8(writer, node.Title);
            WriteUtf8(writer, "</span>"u8);
        }
        else
        {
            WriteUtf8(writer, "<a class=\"md-tabs__link\" href=\""u8);
            WriteRootRelativeHref(writer, href, appendTrailingSlash);
            WriteUtf8(writer, "\">"u8);
            WriteUtf8(writer, node.Title);
            WriteUtf8(writer, "</a>"u8);
        }

        WriteUtf8(writer, "</li>"u8);
    }

    /// <summary>Writes a root-relative href, appending a trailing slash for section-directory fallbacks when required.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="href">Href bytes without the leading slash.</param>
    /// <param name="appendTrailingSlash">True when a slash should be appended if the URL does not already end with one.</param>
    private static void WriteRootRelativeHref(IBufferWriter<byte> writer, ReadOnlySpan<byte> href, bool appendTrailingSlash)
    {
        WriteUtf8(writer, "/"u8);
        if (!href.IsEmpty)
        {
            WriteUtf8(writer, href);
        }

        if (!appendTrailingSlash || (!href.IsEmpty && href[^1] is (byte)'/'))
        {
            return;
        }

        WriteUtf8(writer, "/"u8);
    }

    /// <summary>Picks the URL the tab links to: the section's index page when present, otherwise the section path, or the leaf URL.</summary>
    /// <param name="node">Top-level nav node.</param>
    /// <param name="href">Resolved href bytes without a leading slash.</param>
    /// <param name="appendTrailingSlash">True when the href should end in a slash.</param>
    /// <returns>True when a link is available.</returns>
    private static bool TryGetTabHref(NavNode node, out ReadOnlySpan<byte> href, out bool appendTrailingSlash)
    {
        if (node.IsSection)
        {
            if (node.IndexUrlBytes.Length > 0)
            {
                href = node.IndexUrlBytes;
                appendTrailingSlash = false;
                return true;
            }

            if (node.RelativeUrlBytes.Length > 0)
            {
                href = node.RelativeUrlBytes;
                appendTrailingSlash = true;
                return true;
            }

            href = default;
            appendTrailingSlash = false;
            return false;
        }

        href = node.RelativeUrlBytes;
        appendTrailingSlash = false;
        return true;
    }
}
