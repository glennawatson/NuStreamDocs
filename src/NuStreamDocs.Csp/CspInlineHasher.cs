// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Csp;

/// <summary>Hashes inline <c>&lt;script&gt;</c> / <c>&lt;style&gt;</c> bodies in a page into <c>'sha256-…'</c> CSP source tokens.</summary>
public static class CspInlineHasher
{
    /// <summary>Returns true when <paramref name="html"/> may contain an inline script or style worth hashing.</summary>
    /// <param name="html">Page HTML.</param>
    /// <returns>True when the cheap pre-filter matches.</returns>
    public static bool MayHaveInlineBlocks(ReadOnlySpan<byte> html) =>
        html.IndexOf("<script"u8) >= 0 || html.IndexOf("<style"u8) >= 0;

    /// <summary>Hashes every non-empty inline <c>&lt;script&gt;</c> body in <paramref name="html"/>, appending each <c>'sha256-…'</c> token to <paramref name="hashes"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="hashes">Destination list.</param>
    public static void HashScripts(ReadOnlySpan<byte> html, List<byte[]> hashes) =>
        ScanInto(html, "<script"u8, "</script>"u8, hashes);

    /// <summary>Hashes every non-empty inline <c>&lt;style&gt;</c> body in <paramref name="html"/>, appending each <c>'sha256-…'</c> token to <paramref name="hashes"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="hashes">Destination list.</param>
    public static void HashStyles(ReadOnlySpan<byte> html, List<byte[]> hashes) =>
        ScanInto(html, "<style"u8, "</style>"u8, hashes);

    /// <summary>Hashes every non-empty <c>{open}…&gt;…{close}</c> block body and appends its CSP source token to <paramref name="sink"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="open">Opening-tag prefix.</param>
    /// <param name="close">Closing tag.</param>
    /// <param name="sink">Destination list.</param>
    private static void ScanInto(ReadOnlySpan<byte> html, ReadOnlySpan<byte> open, ReadOnlySpan<byte> close, List<byte[]> sink)
    {
        var blocks = new Utf8InlineBlockEnumerator(html, open, close);
        while (blocks.MoveNext())
        {
            if (blocks.Current.IsEmpty)
            {
                continue;
            }

            sink.Add(CspSourceToken.FromBody(blocks.Current));
        }
    }
}
