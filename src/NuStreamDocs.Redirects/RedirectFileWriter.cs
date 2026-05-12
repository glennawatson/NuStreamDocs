// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Redirects;

/// <summary>Formats redirects into the Netlify / Cloudflare-Pages <c>_redirects</c> file and into per-redirect <c>&lt;meta http-equiv="refresh"&gt;</c> HTML pages.</summary>
public static class RedirectFileWriter
{
    /// <summary>Writes the <c>_redirects</c> file content for <paramref name="rules"/> (one sorted line per rule: <c>&lt;from&gt;  &lt;to&gt;  &lt;301|302&gt;</c>).</summary>
    /// <param name="rules">Redirect rules.</param>
    /// <param name="writer">Destination.</param>
    public static void WriteRedirectsFile(IReadOnlyList<RedirectRule> rules, IBufferWriter<byte> writer)
    {
        var sorted = new RedirectRule[rules.Count];
        for (var i = 0; i < rules.Count; i++)
        {
            sorted[i] = rules[i];
        }

        Array.Sort(sorted, static (a, b) => a.From.AsSpan().SequenceCompareTo(b.From.AsSpan()));
        for (var i = 0; i < sorted.Length; i++)
        {
            writer.Write(sorted[i].From);
            writer.Write("  "u8);
            writer.Write(sorted[i].To);
            writer.Write("  "u8);
            writer.Write(sorted[i].Permanent ? "301"u8 : "302"u8);
            writer.Write("\n"u8);
        }
    }

    /// <summary>Writes a minimal redirect HTML page (immediate <c>meta refresh</c> + <c>canonical</c> + <c>noindex</c>) pointing at <paramref name="to"/>.</summary>
    /// <param name="to">UTF-8 destination URL.</param>
    /// <param name="writer">Destination.</param>
    public static void WriteMetaRefreshHtml(ReadOnlySpan<byte> to, IBufferWriter<byte> writer)
    {
        writer.Write("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"robots\" content=\"noindex\">\n<meta http-equiv=\"refresh\" content=\"0; url="u8);
        writer.Write(to);
        writer.Write("\">\n<link rel=\"canonical\" href=\""u8);
        writer.Write(to);
        writer.Write("\">\n<title>Redirecting…</title>\n</head>\n<body>\n<p>This page has moved to <a href=\""u8);
        writer.Write(to);
        writer.Write("\">"u8);
        writer.Write(to);
        writer.Write("</a>.</p>\n</body>\n</html>\n"u8);
    }
}
