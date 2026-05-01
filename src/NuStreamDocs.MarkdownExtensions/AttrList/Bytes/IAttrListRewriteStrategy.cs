// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Static strategy contract for the attr-list byte scanners.
/// </summary>
/// <typeparam name="TSelf">Concrete strategy type.</typeparam>
internal interface IAttrListRewriteStrategy<TSelf>
    where TSelf : struct, IAttrListRewriteStrategy<TSelf>
{
    /// <summary>Attempts a rewrite at <paramref name="lt"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="lt">Offset of the current <c>&lt;</c>.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when the element was rewritten.</returns>
    static abstract bool TryRewriteAt(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo);
}
