// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Mermaid;

/// <summary>
/// Stateless retagger that rewrites
/// <c>&lt;pre&gt;&lt;code class="language-mermaid"&gt;…&lt;/code&gt;&lt;/pre&gt;</c>
/// blocks to <c>&lt;pre class="mermaid"&gt;…&lt;/pre&gt;</c>.
/// </summary>
internal static class MermaidRetagger
{
    /// <summary>Gets the marker the renderer emits for fenced mermaid blocks.</summary>
    private static ReadOnlySpan<byte> OpenMarker => "<pre><code class=\"language-mermaid\">"u8;

    /// <summary>Gets the matching close marker.</summary>
    private static ReadOnlySpan<byte> CloseMarker => "</code></pre>"u8;

    /// <summary>Gets the replacement open tag.</summary>
    private static ReadOnlySpan<byte> NewOpen => "<pre class=\"mermaid\">"u8;

    /// <summary>Gets the replacement close tag.</summary>
    private static ReadOnlySpan<byte> NewClose => "</pre>"u8;

    /// <summary>Returns true when <paramref name="html"/> contains at least one mermaid block.</summary>
    /// <param name="html">Page HTML span.</param>
    /// <returns>True when the open marker is present.</returns>
    public static bool NeedsRetag(ReadOnlySpan<byte> html) => html.IndexOf(OpenMarker) >= 0;

    /// <summary>Retags every mermaid block in <paramref name="html"/> directly into <paramref name="output"/>.</summary>
    /// <param name="html">Page HTML span.</param>
    /// <param name="output">Destination sink — receives the rewritten bytes verbatim, no intermediate <see cref="byte"/> array.</param>
    public static void Retag(ReadOnlySpan<byte> html, IBufferWriter<byte> output)
    {
        var i = 0;
        while (i < html.Length)
        {
            var rel = html[i..].IndexOf(OpenMarker);
            if (rel < 0)
            {
                output.Write(html[i..]);
                return;
            }

            output.Write(html.Slice(i, rel));
            i += rel + OpenMarker.Length;

            var closeRel = html[i..].IndexOf(CloseMarker);
            if (closeRel < 0)
            {
                output.Write(OpenMarker);
                output.Write(html[i..]);
                return;
            }

            output.Write(NewOpen);
            output.Write(html.Slice(i, closeRel));
            output.Write(NewClose);
            i += closeRel + CloseMarker.Length;
        }
    }
}
