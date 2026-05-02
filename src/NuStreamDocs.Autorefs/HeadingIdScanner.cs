// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Scans rendered HTML bytes for heading <c>id="..."</c> attributes and
/// publishes them to an <see cref="AutorefsRegistry"/>.
/// </summary>
/// <remarks>
/// Byte-only walk on top of <see cref="Utf8HtmlScanner"/>: the
/// renderer's own emitter is the one authoritative source of these
/// tags so the shape is stable, no full HTML parser is needed.
/// The page URL is encoded once per page; per-heading work then
/// stays on byte spans and never decodes to <see cref="string"/>.
/// </remarks>
public static class HeadingIdScanner
{
    /// <summary>Scans <paramref name="html"/> and registers every heading ID it finds.</summary>
    /// <param name="html">UTF-8 rendered HTML bytes.</param>
    /// <param name="pageRelativeUrl">URL the page will be served at, relative to the site root.</param>
    /// <param name="registry">Registry to publish into.</param>
    public static void ScanAndRegister(ReadOnlySpan<byte> html, string pageRelativeUrl, AutorefsRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrEmpty(pageRelativeUrl);

        // One UTF-8 encode per page; every heading registered against this page reuses the same byte[].
        var pageUrlBytes = Encoding.UTF8.GetBytes(pageRelativeUrl);

        var cursor = 0;
        while (cursor < html.Length
               && Utf8HtmlScanner.TryFindNextHeadingOpen(html, cursor, out var tagStart, out var tagEnd, out _))
        {
            var openTag = html[tagStart..tagEnd];
            var (idLocalStart, idLength) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
            if (idLength > 0)
            {
                var idSpan = openTag.Slice(idLocalStart, idLength);
                registry.Register(idSpan, pageUrlBytes, idSpan);
            }

            cursor = tagEnd;
        }
    }
}
