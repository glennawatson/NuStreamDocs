// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy;

/// <summary>Hashes inline <c>&lt;style&gt;</c> and <c>&lt;script&gt;</c> bodies into <c>'sha256-…'</c> CSP source tokens.</summary>
internal static class CspHashCollector
{
    /// <summary>Returns true when <paramref name="html"/> contains any inline style or script tag worth hashing.</summary>
    /// <param name="html">Page HTML.</param>
    /// <returns>True when the cheap pre-filter matches.</returns>
    public static bool MayHaveInlineBlocks(ReadOnlySpan<byte> html) =>
        html.IndexOf("<style"u8) >= 0 || html.IndexOf("<script"u8) >= 0;

    /// <summary>Hashes every inline style and script body in <paramref name="html"/> and adds the formatted CSP source to <paramref name="styles"/> / <paramref name="scripts"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="styles">Sink for <c>'sha256-…'</c> tokens from inline <c>&lt;style&gt;</c> blocks.</param>
    /// <param name="scripts">Sink for <c>'sha256-…'</c> tokens from inline <c>&lt;script&gt;</c> blocks.</param>
    public static void Collect(
        ReadOnlySpan<byte> html,
        ConcurrentDictionary<byte[], byte> styles,
        ConcurrentDictionary<byte[], byte> scripts)
    {
        ScanInto(html, "<style"u8, "</style>"u8, styles);
        ScanInto(html, "<script"u8, "</script>"u8, scripts);
    }

    /// <summary>Hashes every non-empty <c>{open}…&gt;…{close}</c> block body and adds its CSP source token to <paramref name="sink"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="open">Opening-tag prefix (e.g. <c>&lt;style</c>).</param>
    /// <param name="close">Closing tag (e.g. <c>&lt;/style&gt;</c>).</param>
    /// <param name="sink">Output set.</param>
    private static void ScanInto(
        ReadOnlySpan<byte> html,
        ReadOnlySpan<byte> open,
        ReadOnlySpan<byte> close,
        ConcurrentDictionary<byte[], byte> sink)
    {
        var blocks = new Utf8InlineBlockEnumerator(html, open, close);
        while (blocks.MoveNext())
        {
            if (blocks.Current.IsEmpty)
            {
                continue;
            }

            sink.TryAdd(CspSourceToken.FromBody(blocks.Current), 0);
        }
    }
}
