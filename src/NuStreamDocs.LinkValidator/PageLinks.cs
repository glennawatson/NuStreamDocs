// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Per-page link inventory captured during the single corpus-build pass.
/// </summary>
/// <remarks>
/// Storage is byte-only: page URL, link values, and anchor IDs are
/// raw UTF-8 byte arrays. The corpus and validators consume them
/// directly without UTF-16 decoding; only the diagnostic message and
/// external-URL <see cref="System.Uri"/> construction layers cross the
/// string boundary. <see cref="AnchorIds"/> is a plain
/// <see cref="HashSet{T}"/> keyed on a byte-array comparer rather
/// than a frozen set — at site scale anchor sets are built per page
/// (so build cost is paid N times) but queried only as many times as
/// that page has fragment-bearing inbound links (often zero), so the
/// frozen-set build amortization never breaks even.
/// </remarks>
/// <param name="PageUrl">Site-relative URL of the page (forward-slashed UTF-8 bytes, with <c>.html</c>).</param>
/// <param name="InternalLinks">Relative <c>href</c>s + page-local <c>#fragment</c>s as raw UTF-8 byte arrays.</param>
/// <param name="ExternalLinks">Absolute <c>http://</c> / <c>https://</c> hrefs as raw UTF-8 byte arrays.</param>
/// <param name="AnchorIds">Heading <c>id</c>s on this page, byte-array keyed.</param>
public sealed record PageLinks(
    byte[] PageUrl,
    byte[][] InternalLinks,
    byte[][] ExternalLinks,
    HashSet<byte[]> AnchorIds);
