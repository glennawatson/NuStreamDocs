// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator;

/// <summary>Per-page link inventory captured during the corpus build.</summary>
/// <param name="PageUrl">Site-relative URL of the page (forward-slashed UTF-8 bytes, with <c>.html</c>).</param>
/// <param name="InternalLinks">Relative <c>href</c>s + page-local <c>#fragment</c>s as raw UTF-8 byte arrays.</param>
/// <param name="ExternalLinks">Absolute <c>http://</c> / <c>https://</c> hrefs as raw UTF-8 byte arrays.</param>
/// <param name="InternalAssets">
/// Relative <c>src</c> / asset-extension <c>href</c> values pointing at local files (images, fonts,
/// PDFs, CSS, JS) — checked against the on-disk asset corpus rather than the page corpus.
/// </param>
/// <param name="AnchorIds">Heading <c>id</c>s on this page, byte-array keyed.</param>
/// <param name="DeprecatedNameAnchors">
/// Values of obsolete HTML4 <c>&lt;a name="..."&gt;</c> elements present on the page; surfaced as a
/// specific deprecation diagnostic when a fragment resolves through one.
/// </param>
public sealed record PageLinks(
    byte[] PageUrl,
    byte[][] InternalLinks,
    byte[][] ExternalLinks,
    byte[][] InternalAssets,
    HashSet<byte[]> AnchorIds,
    HashSet<byte[]> DeprecatedNameAnchors);
