// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

    /// <summary>Retags every mermaid block in <paramref name="html"/>.</summary>
    /// <param name="html">Page HTML span.</param>
    /// <returns>The rewritten bytes.</returns>
    public static byte[] Retag(ReadOnlySpan<byte> html)
    {
        var output = new byte[html.Length];
        var written = 0;
        var i = 0;
        while (i < html.Length)
        {
            var rel = html[i..].IndexOf(OpenMarker);
            if (rel < 0)
            {
                CopyTo(html[i..], output, ref written);
                break;
            }

            CopyTo(html.Slice(i, rel), output, ref written);
            i += rel + OpenMarker.Length;

            var closeRel = html[i..].IndexOf(CloseMarker);
            if (closeRel < 0)
            {
                CopyTo(OpenMarker, output, ref written);
                CopyTo(html[i..], output, ref written);
                break;
            }

            CopyTo(NewOpen, output, ref written);
            CopyTo(html.Slice(i, closeRel), output, ref written);
            CopyTo(NewClose, output, ref written);
            i += closeRel + CloseMarker.Length;
        }

        if (written == output.Length)
        {
            return output;
        }

        var trimmed = new byte[written];
        output.AsSpan(0, written).CopyTo(trimmed);
        return trimmed;
    }

    /// <summary>Appends <paramref name="src"/> to <paramref name="dest"/> at <paramref name="written"/>.</summary>
    /// <param name="src">Source span.</param>
    /// <param name="dest">Destination buffer.</param>
    /// <param name="written">Cursor; advanced by <paramref name="src"/>.Length.</param>
    private static void CopyTo(ReadOnlySpan<byte> src, byte[] dest, ref int written)
    {
        src.CopyTo(dest.AsSpan(written));
        written += src.Length;
    }
}
