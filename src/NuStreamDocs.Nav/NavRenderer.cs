// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
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
/// Both paths walk the flat <see cref="NavTree"/> by integer indices, write directly into the
/// supplied <see cref="IBufferWriter{T}"/>, and never allocate a per-render buffer — the
/// active-branch chain lives on the stack as a <see cref="Span{T}"/> of <c>int</c>. HTML attribute
/// values come from controlled inputs (URLs we built, titles from frontmatter or filenames), so
/// they're emitted without escaping; if the renderer ever takes untrusted input, the attribute
/// writer needs encoding added.
/// </remarks>
internal static class NavRenderer
{
    /// <summary>Stack-buffer slot count for the active-branch chain. ≥ realistic max nav depth on real corpora.</summary>
    private const int ActiveBranchStackBufferSize = 16;

    /// <summary>Sentinel used for "no parent" (root) and "no children" (leaf) and "no active node".</summary>
    private const int NoIndex = -1;

    /// <summary>Emits the full nav tree, marking the active branch derived from <paramref name="activeIndex"/>.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="activeIndex">Index of the active node in <paramref name="tree"/>, or <c>-1</c> when no page is active.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [SuppressMessage("Roslynator", "RCS1118:Mark local variable as const", Justification = "Mutated through 'ref' chained call sites; analyzer false positive.")]
    public static void RenderFull(NavTree tree, int activeIndex, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(writer);

        Span<int> chainBuffer = stackalloc int[ActiveBranchStackBufferSize];
        var chain = BuildActiveBranchChain(tree, activeIndex, chainBuffer);
        var ctx = new NavRenderContext(tree, chain, prune: false, writer);
        var toggleCounter = 0;
        var root = tree.Nodes[NavTree.RootIndex];
        WriteList(in ctx, root.FirstChildIndex, root.ChildCount, level: 0, ref toggleCounter);
    }

    /// <summary>Emits the primary sidebar tree, scoping to the active top-level section when one is selected.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="activeIndex">Active node index, or <c>-1</c> when no page is active.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [SuppressMessage("Roslynator", "RCS1118:Mark local variable as const", Justification = "Mutated through 'ref' chained call sites; analyzer false positive.")]
    public static void RenderSidebarFull(NavTree tree, int activeIndex, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(writer);

        Span<int> chainBuffer = stackalloc int[ActiveBranchStackBufferSize];
        var chain = BuildActiveBranchChain(tree, activeIndex, chainBuffer);
        var ctx = new NavRenderContext(tree, chain, prune: false, writer);
        var toggleCounter = 0;
        WriteSidebar(in ctx, ref toggleCounter);
    }

    /// <summary>Emits the pruned nav: only the active branch and its immediate context.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="activeIndex">Active node index, or <c>-1</c> when no page is active.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [SuppressMessage("Roslynator", "RCS1118:Mark local variable as const", Justification = "Mutated through 'ref' chained call sites; analyzer false positive.")]
    public static void RenderPruned(NavTree tree, int activeIndex, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(writer);

        Span<int> chainBuffer = stackalloc int[ActiveBranchStackBufferSize];
        var chain = BuildActiveBranchChain(tree, activeIndex, chainBuffer);
        var ctx = new NavRenderContext(tree, chain, prune: true, writer);
        var toggleCounter = 0;
        var root = tree.Nodes[NavTree.RootIndex];
        WriteList(in ctx, root.FirstChildIndex, root.ChildCount, level: 0, ref toggleCounter);
    }

    /// <summary>Emits the pruned primary sidebar tree.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="activeIndex">Active node index, or <c>-1</c> when no page is active.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [SuppressMessage("Roslynator", "RCS1118:Mark local variable as const", Justification = "Mutated through 'ref' chained call sites; analyzer false positive.")]
    public static void RenderSidebarPruned(NavTree tree, int activeIndex, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(writer);

        Span<int> chainBuffer = stackalloc int[ActiveBranchStackBufferSize];
        var chain = BuildActiveBranchChain(tree, activeIndex, chainBuffer);
        var ctx = new NavRenderContext(tree, chain, prune: true, writer);
        var toggleCounter = 0;
        WriteSidebar(in ctx, ref toggleCounter);
    }

    /// <summary>Emits a horizontal tab bar from the root's top-level children.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="activeIndex">Active node index, or <c>-1</c> when no page is active.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RenderTabs(NavTree tree, int activeIndex, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(writer);

        Span<int> chainBuffer = stackalloc int[ActiveBranchStackBufferSize];
        var chain = BuildActiveBranchChain(tree, activeIndex, chainBuffer);

        WriteUtf8(writer, "<nav class=\"md-tabs\" aria-label=\"Tabs\" data-md-component=\"tabs\"><div class=\"md-tabs__inner md-grid\"><ul class=\"md-tabs__list\">"u8);
        var root = tree.Nodes[NavTree.RootIndex];
        for (var i = 0; i < root.ChildCount; i++)
        {
            var childIdx = root.FirstChildIndex + i;
            if (IsTopLevelHomePage(tree.Nodes[childIdx]))
            {
                continue;
            }

            WriteTabItem(tree, childIdx, chain, writer);
        }

        WriteUtf8(writer, "</ul></div></nav>"u8);
    }

    /// <summary>Indexes every node in <paramref name="tree"/> by UTF-8 URL bytes so per-page rendering can resolve the active node in O(1).</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <returns>UTF-8 URL bytes → node index map.</returns>
    public static Dictionary<byte[], int> BuildUrlIndex(NavTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var index = new Dictionary<byte[], int>(tree.Nodes.Length, ByteArrayComparer.Instance);
        var nodes = tree.Nodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (!node.IsSection && node.RelativeUrlBytes.Length > 0)
            {
                index[node.RelativeUrlBytes] = i;
            }

            if (node.IsSection && node.IndexUrlBytes.Length > 0)
            {
                index[node.IndexUrlBytes] = i;
            }
        }

        return index;
    }

    /// <summary>Builds the active-ancestor chain into <paramref name="buffer"/> and returns the populated slice.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="activeIndex">Active node index, or <c>-1</c> when no page is active.</param>
    /// <param name="buffer">Stack-allocated buffer; depths beyond <see cref="ActiveBranchStackBufferSize"/> are silently clipped — real corpora cap out around 8.</param>
    /// <returns>Populated slice; empty when <paramref name="activeIndex"/> is <c>-1</c>.</returns>
    private static ReadOnlySpan<int> BuildActiveBranchChain(NavTree tree, int activeIndex, Span<int> buffer)
    {
        if (activeIndex < 0)
        {
            return [];
        }

        var count = 0;
        var current = activeIndex;
        var nodes = tree.Nodes;
        while (current >= 0 && count < buffer.Length)
        {
            buffer[count++] = current;
            current = nodes[current].ParentIndex;
        }

        return buffer[..count];
    }

    /// <summary>Returns true when <paramref name="nodeIndex"/> sits on the active branch.</summary>
    /// <param name="chain">Stack-built active-branch chain.</param>
    /// <param name="nodeIndex">Candidate index.</param>
    /// <returns>True when the candidate is in the chain.</returns>
    private static bool IsOnActiveBranch(ReadOnlySpan<int> chain, int nodeIndex)
    {
        for (var i = 0; i < chain.Length; i++)
        {
            if (chain[i] == nodeIndex)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns true when <paramref name="node"/> is a top-level <c>index.md</c> / <c>README.md</c> leaf that should not be emitted as its own tab.</summary>
    /// <param name="node">Top-level child of the nav root.</param>
    /// <returns>True when the node is the implicit home page; false otherwise.</returns>
    private static bool IsTopLevelHomePage(in NavTreeNode node)
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

        if (relative.AsSpan().IndexOfAny(['/', '\\']) >= 0)
        {
            return false;
        }

        return string.Equals(relative, "index.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(relative, "README.md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resolves the scoped primary-sidebar item range for the active page and emits the resulting list.</summary>
    /// <param name="ctx">Render context.</param>
    /// <param name="toggleCounter">Per-render section-toggle counter.</param>
    private static void WriteSidebar(in NavRenderContext ctx, ref int toggleCounter)
    {
        var root = ctx.Tree.Nodes[NavTree.RootIndex];
        var homeIdx = NoIndex;
        var activeSectionIdx = NoIndex;
        for (var i = 0; i < root.ChildCount; i++)
        {
            var childIdx = root.FirstChildIndex + i;
            var child = ctx.Tree.Nodes[childIdx];
            if (homeIdx < 0 && IsTopLevelHomePage(in child))
            {
                homeIdx = childIdx;
                continue;
            }

            if (activeSectionIdx < 0 && child.IsSection && IsOnActiveBranch(ctx.Chain, childIdx))
            {
                activeSectionIdx = childIdx;
            }
        }

        if (activeSectionIdx < 0)
        {
            WriteList(in ctx, root.FirstChildIndex, root.ChildCount, level: 0, ref toggleCounter);
            return;
        }

        WriteScopedSidebar(in ctx, homeIdx, activeSectionIdx, ref toggleCounter);
    }

    /// <summary>Writes the contextual sidebar that scopes to the active top-level section, optionally hoisting a single-leaf wrapper.</summary>
    /// <param name="ctx">Render context.</param>
    /// <param name="homeIdx">Top-level home leaf index, or <c>-1</c> when there is none.</param>
    /// <param name="activeSectionIdx">Active top-level section index.</param>
    /// <param name="toggleCounter">Per-render section-toggle counter.</param>
    private static void WriteScopedSidebar(in NavRenderContext ctx, int homeIdx, int activeSectionIdx, ref int toggleCounter)
    {
        var activeSection = ctx.Tree.Nodes[activeSectionIdx];

        // Single-leaf wrapper: hoist children up so the sidebar doesn't render the redundant
        // section header around a one-page subtree.
        if (activeSection.ChildCount is 1 && !ctx.Tree.Nodes[activeSection.FirstChildIndex].IsSection)
        {
            WriteUtf8(ctx.Writer, "<ul class=\"md-nav__list\" data-md-scrollfix>"u8);
            if (homeIdx >= 0)
            {
                WriteItem(in ctx, homeIdx, level: 0, ref toggleCounter);
            }

            for (var i = 0; i < activeSection.ChildCount; i++)
            {
                WriteItem(in ctx, activeSection.FirstChildIndex + i, level: 0, ref toggleCounter);
            }

            WriteUtf8(ctx.Writer, "</ul>"u8);
            return;
        }

        WriteUtf8(ctx.Writer, "<ul class=\"md-nav__list\" data-md-scrollfix>"u8);
        if (homeIdx >= 0)
        {
            WriteItem(in ctx, homeIdx, level: 0, ref toggleCounter);
        }

        WriteItem(in ctx, activeSectionIdx, level: 0, ref toggleCounter);
        WriteUtf8(ctx.Writer, "</ul>"u8);
    }

    /// <summary>Writes a list of items in a span over <see cref="NavTree.Nodes"/>.</summary>
    /// <param name="ctx">Render context.</param>
    /// <param name="firstChild">Starting index, or <c>-1</c> for empty.</param>
    /// <param name="childCount">Number of items to render.</param>
    /// <param name="level">Current nav depth.</param>
    /// <param name="toggleCounter">Per-render section-toggle counter.</param>
    private static void WriteList(in NavRenderContext ctx, int firstChild, int childCount, int level, ref int toggleCounter)
    {
        if (childCount <= 0 || firstChild < 0)
        {
            return;
        }

        WriteUtf8(ctx.Writer, "<ul class=\"md-nav__list\" data-md-scrollfix>"u8);
        for (var i = 0; i < childCount; i++)
        {
            WriteItem(in ctx, firstChild + i, level, ref toggleCounter);
        }

        WriteUtf8(ctx.Writer, "</ul>"u8);
    }

    /// <summary>Writes one <c>&lt;li&gt;</c> for either a section or a leaf page.</summary>
    /// <param name="ctx">Render context.</param>
    /// <param name="nodeIndex">Index of the node to render.</param>
    /// <param name="level">Current nav depth.</param>
    /// <param name="toggleCounter">Per-render section-toggle counter.</param>
    private static void WriteItem(in NavRenderContext ctx, int nodeIndex, int level, ref int toggleCounter)
    {
        var node = ctx.Tree.Nodes[nodeIndex];
        var active = IsOnActiveBranch(ctx.Chain, nodeIndex);
        WriteUtf8(ctx.Writer, ResolveItemOpenTag(in node, active, ctx.Prune));

        if (node.IsSection)
        {
            WriteSection(in ctx, nodeIndex, active, level, ref toggleCounter);
        }
        else
        {
            WriteLeaf(in node, active, ctx.Writer);
        }

        WriteUtf8(ctx.Writer, "</li>"u8);
    }

    /// <summary>Returns the right opening <c>&lt;li&gt;</c> tag for <paramref name="node"/>.</summary>
    /// <param name="node">Node being rendered.</param>
    /// <param name="active">True when the node sits on the active branch.</param>
    /// <param name="prune">True when the tree is being rendered in prune mode.</param>
    /// <returns>UTF-8 bytes for the opening tag.</returns>
    private static ReadOnlySpan<byte> ResolveItemOpenTag(in NavTreeNode node, bool active, bool prune)
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

    /// <summary>Writes a section node's expandable toggle + link + nested list, or a leaf-style chevron link.</summary>
    /// <param name="ctx">Render context.</param>
    /// <param name="nodeIndex">Index of the section node.</param>
    /// <param name="active">True when the section sits on the active branch.</param>
    /// <param name="level">Current nav depth.</param>
    /// <param name="toggleCounter">Per-render section-toggle counter.</param>
    private static void WriteSection(in NavRenderContext ctx, int nodeIndex, bool active, int level, ref int toggleCounter)
    {
        var node = ctx.Tree.Nodes[nodeIndex];
        var hasChildren = ShouldEmitChildren(in node, ctx.Prune, active);
        if (hasChildren)
        {
            WriteSectionWithToggle(in ctx, nodeIndex, active, level, ref toggleCounter);
            return;
        }

        WriteSectionLink(ctx.Tree, nodeIndex, active, includeChevron: ctx.Prune && !active && node.ChildCount > 0, ctx.Writer);
    }

    /// <summary>Writes the section in the full expandable shape: hidden toggle + link container + chevron label + nested nav.</summary>
    /// <param name="ctx">Render context.</param>
    /// <param name="nodeIndex">Section node index.</param>
    /// <param name="active">True when the section sits on the active branch.</param>
    /// <param name="level">Current nav depth.</param>
    /// <param name="toggleCounter">Per-render toggle counter.</param>
    private static void WriteSectionWithToggle(in NavRenderContext ctx, int nodeIndex, bool active, int level, ref int toggleCounter)
    {
        toggleCounter++;
        var toggleId = toggleCounter;
        var writer = ctx.Writer;
        var node = ctx.Tree.Nodes[nodeIndex];

        WriteUtf8(writer, "<input class=\"md-nav__toggle md-toggle\" type=\"checkbox\" id=\"__nav_"u8);
        WriteLevel(writer, toggleId);
        WriteUtf8(writer, active ? "\" checked>"u8 : "\">"u8);

        WriteUtf8(writer, "<div class=\"md-nav__link md-nav__container\">"u8);
        WriteSectionLink(ctx.Tree, nodeIndex, active, includeChevron: false, writer);
        WriteUtf8(writer, active ? "<label class=\"md-nav__link md-nav__link--active\" for=\"__nav_"u8 : "<label class=\"md-nav__link\" for=\"__nav_"u8);
        WriteLevel(writer, toggleId);
        WriteUtf8(writer, "\" id=\"__nav_"u8);
        WriteLevel(writer, toggleId);
        WriteUtf8(writer, "_label\" tabindex=\"\"><span class=\"md-nav__icon md-icon\"></span></label></div>"u8);

        WriteUtf8(writer, "<nav class=\"md-nav\" data-md-level=\""u8);
        WriteLevel(writer, level + 1);
        WriteUtf8(writer, "\" aria-labelledby=\"__nav_"u8);
        WriteLevel(writer, toggleId);
        WriteUtf8(writer, active ? "_label\" aria-expanded=\"true\">"u8 : "_label\">"u8);
        WriteUtf8(writer, "<label class=\"md-nav__title\" for=\"__nav_"u8);
        WriteLevel(writer, toggleId);
        WriteUtf8(writer, "\"><span class=\"md-nav__icon md-icon\"></span>"u8);
        WriteUtf8(writer, node.Title);
        WriteUtf8(writer, "</label>"u8);
        WriteList(in ctx, node.FirstChildIndex, node.ChildCount, level + 1, ref toggleCounter);
        WriteUtf8(writer, "</nav>"u8);
    }

    /// <summary>Writes just the section's link (anchor or span) plus title, optionally followed by a leaf-style chevron icon.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="nodeIndex">Section node index.</param>
    /// <param name="active">True when this section's link should render with <c>md-nav__link--active</c>.</param>
    /// <param name="includeChevron">When true, append an <c>md-nav__icon</c> span after the title.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteSectionLink(NavTree tree, int nodeIndex, bool active, bool includeChevron, IBufferWriter<byte> writer)
    {
        var node = tree.Nodes[nodeIndex];
        var hasHref = TryGetSidebarHref(tree, nodeIndex, out var href, out var appendTrailingSlash);
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
        if (includeChevron)
        {
            WriteUtf8(writer, "<span class=\"md-nav__icon md-icon\"></span>"u8);
        }

        WriteUtf8(writer, hasHref ? "</a>"u8 : "</span>"u8);
    }

    /// <summary>True when this section should emit its full expandable shape (children rendered inline).</summary>
    /// <param name="node">Section node.</param>
    /// <param name="prune">Prune-mode flag.</param>
    /// <param name="active">Active-branch flag.</param>
    /// <returns>True when children should be emitted.</returns>
    private static bool ShouldEmitChildren(in NavTreeNode node, bool prune, bool active) =>
        node.ChildCount > 0 && (!prune || active);

    /// <summary>Writes a leaf node's anchor.</summary>
    /// <param name="node">Leaf node.</param>
    /// <param name="active">True when this is the current page.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteLeaf(in NavTreeNode node, bool active, IBufferWriter<byte> writer)
    {
        WriteUtf8(writer, active ? "<a class=\"md-nav__link md-nav__link--active\" href=\""u8 : "<a class=\"md-nav__link\" href=\""u8);
        WriteRootRelativeHref(writer, node.RelativeUrlBytes, appendTrailingSlash: false);
        WriteUtf8(writer, "\">"u8);
        WriteTitleSpan(writer, IsTopLevelHomePage(in node) ? "Home"u8 : node.Title);
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
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="nodeIndex">Section node index.</param>
    /// <param name="href">Resolved href bytes without a leading slash.</param>
    /// <param name="appendTrailingSlash">True when the href should end in a slash.</param>
    /// <returns>True when a link is available.</returns>
    private static bool TryGetSidebarHref(NavTree tree, int nodeIndex, out ReadOnlySpan<byte> href, out bool appendTrailingSlash)
    {
        var node = tree.Nodes[nodeIndex];
        if (node.IndexUrlBytes.Length > 0)
        {
            href = node.IndexUrlBytes;
            appendTrailingSlash = false;
            return true;
        }

        if (TryGetFirstLeafHref(tree, nodeIndex, out href))
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

    /// <summary>Returns the first descendant leaf href for the section at <paramref name="nodeIndex"/>.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="nodeIndex">Parent section index.</param>
    /// <param name="href">Resolved href bytes without a leading slash.</param>
    /// <returns>True when a descendant leaf exists.</returns>
    private static bool TryGetFirstLeafHref(NavTree tree, int nodeIndex, out ReadOnlySpan<byte> href)
    {
        var node = tree.Nodes[nodeIndex];
        for (var i = 0; i < node.ChildCount; i++)
        {
            var childIdx = node.FirstChildIndex + i;
            var child = tree.Nodes[childIdx];
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

            if (TryGetFirstLeafHref(tree, childIdx, out href))
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

    /// <summary>Emits a single <c>&lt;li class="md-tabs__item"&gt;</c> for the node at <paramref name="nodeIndex"/>.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="nodeIndex">Top-level node index.</param>
    /// <param name="chain">Active-branch chain.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteTabItem(NavTree tree, int nodeIndex, ReadOnlySpan<int> chain, IBufferWriter<byte> writer)
    {
        var node = tree.Nodes[nodeIndex];
        var active = IsOnActiveBranch(chain, nodeIndex);
        WriteUtf8(writer, active ? "<li class=\"md-tabs__item md-tabs__item--active\">"u8 : "<li class=\"md-tabs__item\">"u8);

        if (!TryGetTabHref(in node, out var href, out var appendTrailingSlash))
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

    /// <summary>Writes a root-relative href, appending a trailing slash when required.</summary>
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
    /// <param name="node">Materialized node.</param>
    /// <param name="href">Resolved href bytes without a leading slash.</param>
    /// <param name="appendTrailingSlash">True when the href should end in a slash.</param>
    /// <returns>True when a link is available.</returns>
    private static bool TryGetTabHref(in NavTreeNode node, out ReadOnlySpan<byte> href, out bool appendTrailingSlash)
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
