// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>Flat, render-ready nav tree. The renderer walks <see cref="Nodes"/> by index and never dereferences a class instance per node.</summary>
/// <remarks>
/// Built once per build by <see cref="NavTreeFlattener"/>. Children of node at index <c>i</c> live
/// at <c>Nodes[i.FirstChildIndex .. i.FirstChildIndex + i.ChildCount]</c>; parents are walked via
/// <see cref="NavTreeNode.ParentIndex"/>. Root is at index <see cref="RootIndex"/>.
/// </remarks>
internal sealed class NavTree
{
    /// <summary>Index of the root node by convention.</summary>
    public const int RootIndex = 0;

    /// <summary>Initializes a new instance of the <see cref="NavTree"/> class.</summary>
    /// <param name="nodes">BFS-ordered nodes; root must be at index 0.</param>
    public NavTree(NavTreeNode[] nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Length is 0)
        {
            throw new ArgumentException("NavTree requires at least a root node.", nameof(nodes));
        }

        Nodes = nodes;
    }

    /// <summary>Gets the empty nav tree — single section root with no children.</summary>
    public static NavTree Empty { get; } = new(
    [
        new(
            Title: [],
            RelativePath: default,
            IndexPath: default,
            RelativeUrlBytes: [],
            IndexUrlBytes: [],
            ParentIndex: -1,
            FirstChildIndex: -1,
            ChildCount: 0,
            IsSection: true),
    ]);

    /// <summary>Gets the BFS-ordered nodes; index <c>0</c> is the root.</summary>
    public NavTreeNode[] Nodes { get; }

    /// <summary>Gets the children of the node at <paramref name="index"/> as a span over <see cref="Nodes"/>.</summary>
    /// <param name="index">Parent index.</param>
    /// <returns>Span of contiguous children; empty for leaves.</returns>
    public ReadOnlySpan<NavTreeNode> ChildrenOf(int index)
    {
        var node = Nodes[index];
        return node.ChildCount is 0 ? [] : Nodes.AsSpan(node.FirstChildIndex, node.ChildCount);
    }
}
