// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Nav;

/// <summary>Per-render bundle threaded through <see cref="NavRenderer"/>'s helpers.</summary>
internal readonly ref struct NavRenderContext
{
    /// <summary>Initializes a new instance of the <see cref="NavRenderContext"/> struct.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="chain">Active-branch chain over <see cref="NavTree.Nodes"/> indices.</param>
    /// <param name="prune">Prune-mode flag.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public NavRenderContext(NavTree tree, in ReadOnlySpan<int> chain, bool prune, IBufferWriter<byte> writer)
    {
        Tree = tree;
        Chain = chain;
        Prune = prune;
        Writer = writer;
    }

    /// <summary>Gets the flat nav tree.</summary>
    public NavTree Tree { get; }

    /// <summary>Gets the active-branch chain.</summary>
    public ReadOnlySpan<int> Chain { get; }

    /// <summary>Gets a value indicating whether prune mode is active.</summary>
    public bool Prune { get; }

    /// <summary>Gets the UTF-8 sink.</summary>
    public IBufferWriter<byte> Writer { get; }
}
