// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Stateless collector that pulls inline <c>&lt;style&gt;</c> and
/// <c>&lt;script&gt;</c> bodies out of rendered HTML and adds their
/// SHA-256 hashes (formatted for a Content-Security-Policy directive)
/// to a thread-safe set.
/// </summary>
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
    public static void Collect(ReadOnlySpan<byte> html, ConcurrentDictionary<string, byte> styles, ConcurrentDictionary<string, byte> scripts)
    {
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(scripts);

        ScanBlocks(html, "<style"u8, "</style>"u8, styles);
        ScanBlocks(html, "<script"u8, "</script>"u8, scripts);
    }

    /// <summary>Walks <paramref name="html"/> for every <c>{open}…&gt;…{close}</c> block, hashes the body, and adds the formatted CSP source to <paramref name="sink"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="open">Opening-tag prefix (e.g. <c>&lt;style</c>).</param>
    /// <param name="close">Closing tag (e.g. <c>&lt;/style&gt;</c>).</param>
    /// <param name="sink">Output set.</param>
    private static void ScanBlocks(ReadOnlySpan<byte> html, ReadOnlySpan<byte> open, ReadOnlySpan<byte> close, ConcurrentDictionary<string, byte> sink)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf(open);
            if (rel < 0)
            {
                break;
            }

            // Skip past the opening-tag attributes to the closing '>'.
            var tagOpenStart = cursor + rel;
            var afterOpen = html[(tagOpenStart + open.Length)..];
            var tagCloseRel = afterOpen.IndexOf((byte)'>');
            if (tagCloseRel < 0)
            {
                break;
            }

            var bodyStart = tagOpenStart + open.Length + tagCloseRel + 1;
            var endRel = html[bodyStart..].IndexOf(close);
            if (endRel < 0)
            {
                break;
            }

            var body = html.Slice(bodyStart, endRel);
            cursor = bodyStart + endRel + close.Length;

            if (body.IsEmpty)
            {
                continue;
            }

            SHA256.HashData(body, hash);
            sink.TryAdd($"'sha256-{Convert.ToBase64String(hash)}'", 0);
        }
    }
}
