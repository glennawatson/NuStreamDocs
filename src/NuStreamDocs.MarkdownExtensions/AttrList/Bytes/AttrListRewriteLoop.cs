// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Shared scan loop for the attr-list byte rewriters.
/// </summary>
internal static class AttrListRewriteLoop
{
    /// <summary>Walks <paramref name="html"/>, copying through verbatim and delegating each candidate tag open to <typeparamref name="TStrategy"/>.</summary>
    /// <typeparam name="TStrategy">Concrete rewrite strategy.</typeparam>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one element was rewritten.</returns>
    [SuppressMessage(
        "Minor Code Smell",
        "S4018:Generic methods should provide type inference",
        Justification = "The call sites intentionally specify the concrete static strategy type to keep the shared scan loop allocation-free.")]
    public static bool RewriteInto<TStrategy>(ReadOnlySpan<byte> html, IBufferWriter<byte> sink)
        where TStrategy : struct, IAttrListRewriteStrategy<TStrategy>
    {
        ArgumentNullException.ThrowIfNull(sink);

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

            var lt = cursor + rel;
            if (TStrategy.TryRewriteAt(html, lt, sink, ref lastEmit, out var advanceTo))
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
