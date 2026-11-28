// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Social;

/// <summary>Resolved per-page social tag values written into <c>&lt;head&gt;</c>.</summary>
/// <param name="Title">UTF-8 page title.</param>
/// <param name="Description">UTF-8 page summary (empty when none).</param>
/// <param name="CanonicalUrl">UTF-8 canonical page URL (empty when <c>site_url</c> is unset).</param>
/// <param name="ImageUrl">UTF-8 preview image URL (empty when none).</param>
/// <param name="GeneratedSvgImage"><c>true</c> when <paramref name="ImageUrl"/> points at a generated <c>1200x630</c> SVG card.</param>
/// <param name="SiteName">UTF-8 site name (empty when unset).</param>
/// <param name="SiteAuthor">UTF-8 site author (empty when unset).</param>
[System.Diagnostics.DebuggerDisplay("SocialHeadPayload: GeneratedSvg={GeneratedSvgImage}")]
internal readonly record struct SocialHeadPayload(
    byte[] Title,
    byte[] Description,
    byte[] CanonicalUrl,
    byte[] ImageUrl,
    bool GeneratedSvgImage,
    byte[] SiteName,
    byte[] SiteAuthor);
