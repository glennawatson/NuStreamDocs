// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Nav;

/// <summary>Stable per-render bundle threaded through <see cref="NavRenderer"/>'s private helpers to keep their arities small.</summary>
/// <remarks>
/// Ref struct so it can hold a <see cref="ReadOnlySpan{T}"/> chain without escaping. The
/// per-render <c>toggleCounter</c> stays as a separate <c>ref int</c> parameter on the call chain
/// — keeping it out of the bundle simplifies analyzer flow analysis and avoids the
/// <c>ref</c>-field complexity for what's effectively just one mutable counter.
/// </remarks>
internal readonly ref struct NavRenderContext
{
    /// <summary>Initializes a new instance of the <see cref="NavRenderContext"/> struct.</summary>
    /// <param name="tree">Flat nav tree.</param>
    /// <param name="chain">Active-branch chain over <see cref="NavTree.Nodes"/> indices.</param>
    /// <param name="prune">Prune-mode flag.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public NavRenderContext(NavTree tree, ReadOnlySpan<int> chain, bool prune, IBufferWriter<byte> writer)
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
