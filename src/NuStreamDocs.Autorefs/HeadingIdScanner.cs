// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Scans rendered HTML bytes for heading <c>id="..."</c> attributes and
/// publishes them to an <see cref="AutorefsRegistry"/>.
/// </summary>
/// <remarks>
/// Byte-only walk on top of <see cref="Utf8HtmlScanner"/>: the
/// renderer's own emitter is the one authoritative source of these
/// tags so the shape is stable, no full HTML parser is needed. The
/// caller supplies the page URL pre-encoded once per page; per-heading
/// work then stays on byte spans and never decodes to <see cref="string"/>.
/// </remarks>
public static class HeadingIdScanner
{
    /// <summary>Scans <paramref name="html"/> and registers every heading ID it finds.</summary>
    /// <param name="html">UTF-8 rendered HTML bytes.</param>
    /// <param name="pageUrlBytes">
    /// UTF-8 page URL bytes; the same array reference is shared across every heading registered for this page,
    /// so the registry stores it once rather than copying it per heading.
    /// </param>
    /// <param name="registry">Registry to publish into.</param>
    public static void ScanAndRegister(ReadOnlySpan<byte> html, byte[] pageUrlBytes, AutorefsRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(pageUrlBytes);
        if (pageUrlBytes.Length is 0)
        {
            throw new ArgumentException("Page URL must be non-empty.", nameof(pageUrlBytes));
        }

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
