// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SuperFences;

/// <summary>
/// Stateless rendered-HTML rewriter that dispatches matched
/// <c>&lt;pre&gt;&lt;code class="language-{lang}"&gt;…&lt;/code&gt;&lt;/pre&gt;</c>
/// blocks to registered <see cref="ICustomFenceHandler"/>s.
/// </summary>
/// <remarks>
/// Handler lookup is byte-keyed via <see cref="System.Collections.Generic.Dictionary{TKey, TValue}.GetAlternateLookup{TAlternate}()"/>
/// — the language slice off the rendered HTML drives the lookup with
/// no UTF-16 transcoding and no per-fence byte-array allocation. The
/// rewrite path streams straight into the caller's destination sink
/// instead of materializing an intermediate replacement byte array.
/// </remarks>
internal static class SuperFencesDispatcher
{
    /// <summary>Gets the UTF-8 bytes of the prefix every candidate block starts with.</summary>
    private static ReadOnlySpan<byte> Prefix => "<pre><code class=\"language-"u8;

    /// <summary>Gets the bytes that close the <c>&lt;code&gt;</c> tag's class attribute.</summary>
    private static ReadOnlySpan<byte> ClassClose => "\">"u8;

    /// <summary>Gets the UTF-8 bytes that end every candidate block.</summary>
    private static ReadOnlySpan<byte> BlockSuffix => "</code></pre>"u8;

    /// <summary>Returns true when <paramref name="html"/> contains at least one candidate block worth scanning further.</summary>
    /// <param name="html">Rendered HTML.</param>
    /// <returns>True when the prefix is present.</returns>
    public static bool NeedsDispatch(ReadOnlySpan<byte> html) => html.IndexOf(Prefix) >= 0;

    /// <summary>Walks <paramref name="html"/>, dispatching matched fences in <paramref name="handlers"/> directly into <paramref name="sink"/>.</summary>
    /// <param name="html">Rendered HTML bytes.</param>
    /// <param name="handlers">Span-keyed handler lookup (an alternate lookup over a byte-array keyed dictionary).</param>
    /// <param name="sink">UTF-8 sink to receive the rewritten output.</param>
    /// <returns>True when at least one block was dispatched (i.e. the sink content differs from <paramref name="html"/>); false when no candidate matched a handler.</returns>
    public static bool DispatchInto(
        ReadOnlySpan<byte> html,
        in Dictionary<byte[], ICustomFenceHandler>.AlternateLookup<ReadOnlySpan<byte>> handlers,
        IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (html.IsEmpty)
        {
            return false;
        }

        var changed = false;
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf(Prefix);
            if (rel < 0)
            {
                if (changed)
                {
                    sink.Write(html[cursor..]);
                }

                break;
            }

            var blockStart = cursor + rel;
            if (!TryResolveHandler(html, blockStart, handlers, out var handler, out var bodyStart, out var bodyEnd))
            {
                // Not a block we can dispatch. If we've already started rewriting, copy the prefix verbatim so the rest of the page reaches the sink intact.
                var prefixEnd = blockStart + Prefix.Length;
                if (changed)
                {
                    sink.Write(html[cursor..prefixEnd]);
                }

                cursor = prefixEnd;
                continue;
            }

            if (!changed)
            {
                // First match — backfill the verbatim prefix we held off writing so the sink starts with the bytes leading up to the block.
                sink.Write(html[..blockStart]);
                changed = true;
            }
            else
            {
                sink.Write(html[cursor..blockStart]);
            }

            var decoded = HtmlEntityDecoder.Decode(html[bodyStart..bodyEnd]);
            handler.Render(decoded, sink);

            cursor = bodyEnd + BlockSuffix.Length;
        }

        return changed;
    }

    /// <summary>
    /// Probes the block at <paramref name="blockStart"/>: validates
    /// shape, resolves the language to a handler, and returns the body
    /// span. Single hash + single equality call via the .NET 9
    /// <c>Dictionary.AlternateLookup.TryGetValue</c> overload.
    /// </summary>
    /// <param name="html">Rendered HTML.</param>
    /// <param name="blockStart">Offset of the leading <c>&lt;</c>.</param>
    /// <param name="handlers">Span-keyed handler lookup.</param>
    /// <param name="handler">Resolved handler on success.</param>
    /// <param name="bodyStart">Inclusive offset of the body's first byte on success.</param>
    /// <param name="bodyEnd">Exclusive offset of the body's last byte on success.</param>
    /// <returns>True when both the block shape and the language resolve.</returns>
    private static bool TryResolveHandler(
        ReadOnlySpan<byte> html,
        int blockStart,
        in Dictionary<byte[], ICustomFenceHandler>.AlternateLookup<ReadOnlySpan<byte>> handlers,
        out ICustomFenceHandler handler,
        out int bodyStart,
        out int bodyEnd)
    {
        handler = null!;
        bodyStart = 0;
        bodyEnd = 0;

        var langStart = blockStart + Prefix.Length;
        var classCloseRel = html[langStart..].IndexOf(ClassClose);
        if (classCloseRel < 0)
        {
            return false;
        }

        var langEnd = langStart + classCloseRel;
        bodyStart = langEnd + ClassClose.Length;
        var suffixRel = html[bodyStart..].IndexOf(BlockSuffix);
        if (suffixRel < 0)
        {
            return false;
        }

        bodyEnd = bodyStart + suffixRel;
        return handlers.TryGetValue(html[langStart..langEnd], out _, out handler!);
    }
}
