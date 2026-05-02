// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Byte-level scanner for the paired-inline attr-list pattern —
/// <c>&lt;tag attrs&gt;inner&lt;/tag&gt;{: attrs }</c>.
/// </summary>
internal static class InlinePairedAttrListBytes
{
    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every paired-inline element followed by a <c>{: ... }</c> token.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one element was rewritten.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, IBufferWriter<byte> sink) =>
        AttrListRewriteLoop.RewriteInto<Strategy>(html, sink);

    /// <summary>Static dispatch strategy for the shared scan loop.</summary>
    private readonly record struct Strategy : IAttrListRewriteStrategy<Strategy>
    {
        /// <inheritdoc/>
        public static bool TryRewriteAt(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo) =>
            AttrListElementRewriter.TryRewriteInlinePaired(html, lt, sink, ref lastEmit, out advanceTo);
    }
}
