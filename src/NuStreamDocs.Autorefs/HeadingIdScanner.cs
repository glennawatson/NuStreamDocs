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

        ScanHeadings(html, pageUrlBytes, registry);
        ScanAnchorTags(html, pageUrlBytes, registry);
    }

    /// <summary>Registers every <c>&lt;hN id="..."&gt;</c> heading.</summary>
    /// <param name="html">UTF-8 rendered HTML.</param>
    /// <param name="pageUrlBytes">Per-page URL byte array, shared across registrations.</param>
    /// <param name="registry">Registry to publish into.</param>
    private static void ScanHeadings(ReadOnlySpan<byte> html, byte[] pageUrlBytes, AutorefsRegistry registry)
    {
        var cursor = 0;
        while (cursor < html.Length
               && Utf8HtmlScanner.TryFindNextHeadingOpen(html, cursor, out var tagStart, out var tagEnd, out _))
        {
            RegisterIdAttribute(html[tagStart..tagEnd], pageUrlBytes, registry);
            cursor = tagEnd;
        }
    }

    /// <summary>Registers every <c>&lt;a id="..."&gt;</c> empty anchor — the form the <c>[](){#id}</c> markdown shorthand expands to.</summary>
    /// <param name="html">UTF-8 rendered HTML.</param>
    /// <param name="pageUrlBytes">Per-page URL byte array, shared across registrations.</param>
    /// <param name="registry">Registry to publish into.</param>
    private static void ScanAnchorTags(ReadOnlySpan<byte> html, byte[] pageUrlBytes, AutorefsRegistry registry)
    {
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf("<a"u8);
            if (rel < 0)
            {
                return;
            }

            var tagStart = cursor + rel;
            var afterStub = tagStart + 2;
            if (afterStub >= html.Length || !IsTagBoundary(html[afterStub]))
            {
                cursor = afterStub;
                continue;
            }

            var closeRel = html[afterStub..].IndexOf((byte)'>');
            if (closeRel < 0)
            {
                return;
            }

            var tagEnd = afterStub + closeRel + 1;
            RegisterIdAttribute(html[tagStart..tagEnd], pageUrlBytes, registry);
            cursor = tagEnd;
        }
    }

    /// <summary>Looks up the <c>id</c> attribute on <paramref name="openTag"/> and registers it when present.</summary>
    /// <param name="openTag">Bytes from <c>&lt;</c> through <c>&gt;</c> inclusive.</param>
    /// <param name="pageUrlBytes">Per-page URL byte array.</param>
    /// <param name="registry">Registry to publish into.</param>
    private static void RegisterIdAttribute(ReadOnlySpan<byte> openTag, byte[] pageUrlBytes, AutorefsRegistry registry)
    {
        var (idLocalStart, idLength) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
        if (idLength <= 0)
        {
            return;
        }

        var idSpan = openTag.Slice(idLocalStart, idLength);
        registry.Register(idSpan, pageUrlBytes, idSpan);
    }

    /// <summary>
    /// True when <paramref name="b"/> is a valid byte right after a tag-name
    /// (whitespace, <c>&gt;</c>, or <c>/</c>); rules out <c>&lt;abbr&gt;</c>
    /// being misread as an <c>&lt;a&gt;</c> open.
    /// </summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for a tag boundary.</returns>
    private static bool IsTagBoundary(byte b) =>
        b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'>' or (byte)'/';
}
