// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>Shared scan loop for the attr-list byte rewriters.</summary>
internal static class AttrListRewriteLoop
{
    /// <summary>Walks <paramref name="html"/>, copying through verbatim and delegating each candidate tag open to <typeparamref name="TStrategy"/>.</summary>
    /// <typeparam name="TStrategy">Concrete rewrite strategy.</typeparam>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one element was rewritten.</returns>
    internal static bool RewriteInto<TStrategy>(ReadOnlySpan<byte> html, IBufferWriter<byte> sink)
        where TStrategy : struct, IAttrListRewriteStrategy<TStrategy>
    {
        var changed = false;
        var lastEmit = 0;
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf((byte)'<');
            if (rel < 0)
            {
                break;
            }

            if (TStrategy.TryRewriteAt(html, cursor + rel, sink, ref lastEmit, out var advanceTo))
            {
                changed = true;
            }

            cursor = advanceTo;
        }

        if (!changed)
        {
            return false;
        }

        sink.Write(html[lastEmit..]);
        return true;
    }
}
