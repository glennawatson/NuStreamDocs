// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Export;

/// <summary>Rewrites internal cross-page <c>href="..."</c> links inside exported chapters to in-document <c>#nd-page-...</c> anchors.</summary>
internal static class ExportLinkRewriter
{
    /// <summary>Length of <c>.md</c> and <c>../</c> prefixes.</summary>
    private const int ThreeCharSegmentLength = 3;

    /// <summary>Length of <c>./</c> prefix.</summary>
    private const int DotSlashLength = 2;

    /// <summary>Length of <c>/index.html</c> suffix.</summary>
    private const int SlashIndexHtmlLength = 11;

    /// <summary>Gets the double-quoted <c>href="</c> marker scanned in chapter HTML.</summary>
    private static ReadOnlySpan<byte> HrefPrefix => "href=\""u8;

    /// <summary>Builds a lookup dictionary mapping normalized served paths to their <c>nd-page-...</c> section anchor IDs.</summary>
    /// <param name="entries">Sorted export chapters.</param>
    /// <returns>Lookup map from normalized path string to section ID bytes.</returns>
    internal static Dictionary<string, byte[]> BuildRouteLookup(ReadOnlySpan<ExportPageEntry> entries)
    {
        Dictionary<string, byte[]> map = [with(StringComparer.OrdinalIgnoreCase)];
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var served = NormalizeHrefKey(Encoding.UTF8.GetString(entry.ServedUrl));
            map[served] = entry.SectionId;

            var stem = entry.SortKey.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? entry.SortKey[..^ThreeCharSegmentLength]
                : entry.SortKey;
            map[NormalizeHrefKey($"{stem}.html")] = entry.SectionId;
            map[NormalizeHrefKey(stem)] = entry.SectionId;
        }

        return map;
    }

    /// <summary>Rewrites matching internal <c>href="..."</c> attributes in <paramref name="bodyHtml"/> into <paramref name="output"/>.</summary>
    /// <param name="bodyHtml">Chapter HTML bytes.</param>
    /// <param name="routeLookup">Normalized route-to-anchor lookup.</param>
    /// <param name="output">Destination buffer.</param>
    internal static void RewriteLinks(
        ReadOnlySpan<byte> bodyHtml,
        Dictionary<string, byte[]> routeLookup,
        IBufferWriter<byte> output)
    {
        var cursor = 0;
        while (cursor < bodyHtml.Length)
        {
            var rel = bodyHtml[cursor..].IndexOf(HrefPrefix);
            if (rel < 0)
            {
                output.Write(bodyHtml[cursor..]);
                break;
            }

            var valStart = cursor + rel + HrefPrefix.Length;
            output.Write(bodyHtml.Slice(cursor, rel + HrefPrefix.Length));

            var quoteRel = bodyHtml[valStart..].IndexOf((byte)'"');
            if (quoteRel < 0)
            {
                output.Write(bodyHtml[valStart..]);
                break;
            }

            var hrefVal = bodyHtml.Slice(valStart, quoteRel);
            if (TryResolveSectionAnchor(hrefVal, routeLookup, out var anchorBytes))
            {
                output.Write("#"u8);
                output.Write(anchorBytes);
            }
            else
            {
                output.Write(hrefVal);
            }

            output.Write("\""u8);
            cursor = valStart + quoteRel + 1;
        }
    }

    /// <summary>Normalizes a relative or root-relative URL path for dictionary lookup.</summary>
    /// <param name="raw">Raw URL path without fragment.</param>
    /// <returns>Trimmed path key (<c>index</c> for root).</returns>
    internal static string NormalizeHrefKey(string raw)
    {
        var trimmed = raw.Trim();
        while (trimmed.StartsWith("../", StringComparison.Ordinal))
        {
            trimmed = trimmed[ThreeCharSegmentLength..];
        }

        if (trimmed.StartsWith("./", StringComparison.Ordinal))
        {
            trimmed = trimmed[DotSlashLength..];
        }

        trimmed = trimmed.Trim('/');
        if (trimmed.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^SlashIndexHtmlLength];
        }
        else if (string.Equals(trimmed, "index.html", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = string.Empty;
        }

        return string.IsNullOrEmpty(trimmed) ? "index" : trimmed;
    }

    /// <summary>Attempts to resolve an internal <paramref name="href"/> to a chapter anchor.</summary>
    /// <param name="href">Raw <c>href</c> attribute value.</param>
    /// <param name="routeLookup">Route lookup map.</param>
    /// <param name="sectionId">Resolved anchor ID bytes.</param>
    /// <returns><c>true</c> when rewritten.</returns>
    private static bool TryResolveSectionAnchor(
        ReadOnlySpan<byte> href,
        Dictionary<string, byte[]> routeLookup,
        out byte[] sectionId)
    {
        if (href.IsEmpty || href[0] == (byte)'#' || href.IndexOf((byte)':') >= 0 || href.StartsWith("//"u8))
        {
            sectionId = [];
            return false;
        }

        var hashIdx = href.IndexOf((byte)'#');
        var pathPart = hashIdx < 0 ? href : href[..hashIdx];
        var key = NormalizeHrefKey(Encoding.UTF8.GetString(pathPart));
        return routeLookup.TryGetValue(key, out sectionId!);
    }
}
