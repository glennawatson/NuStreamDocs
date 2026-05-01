// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SuperFences;

/// <summary>
/// Stateless rendered-HTML rewriter that dispatches matched
/// <c>&lt;pre&gt;&lt;code class="language-{lang}"&gt;…&lt;/code&gt;&lt;/pre&gt;</c>
/// blocks to registered <see cref="ICustomFenceHandler"/>s.
/// </summary>
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

    /// <summary>Rewrites <paramref name="html"/>, dispatching any matched fences in <paramref name="handlers"/>.</summary>
    /// <param name="html">Rendered HTML bytes.</param>
    /// <param name="handlers">Resolved language-→-handler index.</param>
    /// <returns>The rewritten bytes (or the original verbatim when nothing matches).</returns>
    public static byte[] Dispatch(ReadOnlySpan<byte> html, FrozenDictionary<string, ICustomFenceHandler> handlers)
    {
        if (html.IsEmpty)
        {
            return [];
        }

        var sink = new ArrayBufferWriter<byte>(html.Length);
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf(Prefix);
            if (rel < 0)
            {
                sink.Write(html[cursor..]);
                break;
            }

            var blockStart = cursor + rel;
            sink.Write(html[cursor..blockStart]);

            if (!TryDispatchBlock(html, blockStart, handlers, sink, out var blockEnd))
            {
                // Not a block we can dispatch — copy the prefix verbatim and keep walking.
                var prefixEnd = blockStart + Prefix.Length;
                sink.Write(html[blockStart..prefixEnd]);
                cursor = prefixEnd;
                continue;
            }

            cursor = blockEnd;
        }

        return [.. sink.WrittenSpan];
    }

    /// <summary>Tries to dispatch the block starting at <paramref name="blockStart"/>.</summary>
    /// <param name="html">Rendered HTML.</param>
    /// <param name="blockStart">Offset of the <c>&lt;pre&gt;</c>.</param>
    /// <param name="handlers">Handler index.</param>
    /// <param name="sink">Output sink.</param>
    /// <param name="blockEnd">Exclusive end of the dispatched block on success.</param>
    /// <returns>True when a handler was matched and invoked.</returns>
    private static bool TryDispatchBlock(ReadOnlySpan<byte> html, int blockStart, FrozenDictionary<string, ICustomFenceHandler> handlers, ArrayBufferWriter<byte> sink, out int blockEnd)
    {
        blockEnd = 0;
        var langStart = blockStart + Prefix.Length;
        var classCloseRel = html[langStart..].IndexOf(ClassClose);
        if (classCloseRel < 0)
        {
            return false;
        }

        var langEnd = langStart + classCloseRel;
        var bodyStart = langEnd + ClassClose.Length;
        var suffixRel = html[bodyStart..].IndexOf(BlockSuffix);
        if (suffixRel < 0)
        {
            return false;
        }

        var bodyEnd = bodyStart + suffixRel;
        var language = Encoding.UTF8.GetString(html[langStart..langEnd]);
        if (!handlers.TryGetValue(language, out var handler))
        {
            return false;
        }

        var decoded = HtmlEntityDecoder.Decode(html[bodyStart..bodyEnd]);
        handler.Render(decoded, sink);
        blockEnd = bodyEnd + BlockSuffix.Length;
        return true;
    }
}
