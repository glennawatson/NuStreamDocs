// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>Converts a build-time <see cref="NavNode"/> graph into the flat <see cref="NavTree"/> consumed by the renderer.</summary>
/// <remarks>
/// BFS layout: each node's direct children sit in a contiguous span inside
/// <see cref="NavTree.Nodes"/>, so the renderer's child iteration is a span slice rather than a
/// per-step class dereference. Run once per build during <see cref="NavPlugin.DiscoverAsync"/>.
/// </remarks>
internal static class NavTreeFlattener
{
    /// <summary>Default capacity hint for the working list.</summary>
    private const int DefaultCapacity = 256;

    /// <summary>Flattens the build-time tree rooted at <paramref name="root"/> into a <see cref="NavTree"/>.</summary>
    /// <param name="root">Build-time root node.</param>
    /// <returns>Flat nav tree; root sits at index <c>0</c>.</returns>
    public static NavTree Flatten(NavNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var capacity = EstimateCount(root);
        var nodes = new List<NavTreeNode>(capacity);
        var queue = new Queue<(int OwnIndex, NavNode Node)>(capacity);

        nodes.Add(BuildSlot(root, parentIndex: -1));
        queue.Enqueue((0, root));

        while (queue.TryDequeue(out var entry))
        {
            var children = entry.Node.Children;
            if (children.Length is 0)
            {
                continue;
            }

            var firstChild = nodes.Count;
            for (var i = 0; i < children.Length; i++)
            {
                var childIdx = nodes.Count;
                nodes.Add(BuildSlot(children[i], parentIndex: entry.OwnIndex));
                queue.Enqueue((childIdx, children[i]));
            }

            // Patch parent's child range now that all direct children are appended contiguously.
            var slot = nodes[entry.OwnIndex];
            nodes[entry.OwnIndex] = slot with { FirstChildIndex = firstChild, ChildCount = children.Length };
        }

        return new([.. nodes]);
    }

    /// <summary>Flattens <paramref name="root"/> and resolves <paramref name="active"/>'s position in the flat tree in one pass.</summary>
    /// <param name="root">Build-time root node.</param>
    /// <param name="active">Build-time active node, or null when no page is active.</param>
    /// <returns>The flat tree and the active node's index (<c>-1</c> when <paramref name="active"/> is null or absent).</returns>
    public static (NavTree Tree, int ActiveIndex) FlattenWithActive(NavNode root, NavNode? active)
    {
        var tree = Flatten(root);
        if (active is null)
        {
            return (tree, -1);
        }

        var nodes = tree.Nodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].RelativePath.Value == active.RelativePath.Value
                && nodes[i].IsSection == active.IsSection)
            {
                return (tree, i);
            }
        }

        return (tree, -1);
    }

    /// <summary>Builds a flat slot from a build-time node; child range is patched later in BFS.</summary>
    /// <param name="node">Build-time node.</param>
    /// <param name="parentIndex">Parent slot index, or <c>-1</c> for the root.</param>
    /// <returns>Populated slot with no child-range yet.</returns>
    private static NavTreeNode BuildSlot(NavNode node, int parentIndex) =>
        new(
            Title: node.Title,
            RelativePath: node.RelativePath,
            IndexPath: node.IndexPath,
            RelativeUrlBytes: node.RelativeUrlBytes,
            IndexUrlBytes: node.IndexUrlBytes,
            ParentIndex: parentIndex,
            FirstChildIndex: -1,
            ChildCount: 0,
            IsSection: node.IsSection);

    /// <summary>Counts the build-time tree's total node count for a one-shot list capacity.</summary>
    /// <param name="root">Build-time root node.</param>
    /// <returns>Total node count, with a small floor to absorb tiny trees.</returns>
    private static int EstimateCount(NavNode root)
    {
        var count = 0;
        CountVisit(root, ref count);
        return count > DefaultCapacity ? count : DefaultCapacity;
    }

    /// <summary>Pre-order count helper for <see cref="EstimateCount"/>.</summary>
    /// <param name="node">Current build-time node.</param>
    /// <param name="count">Accumulator.</param>
    private static void CountVisit(NavNode node, ref int count)
    {
        count++;
        var children = node.Children;
        for (var i = 0; i < children.Length; i++)
        {
            CountVisit(children[i], ref count);
        }
    }
}
