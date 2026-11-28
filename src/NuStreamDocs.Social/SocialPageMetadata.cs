// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Social;

/// <summary>Per-page metadata extracted from Markdown frontmatter and body prose for social cards and structured data.</summary>
/// <param name="Title">Resolved UTF-8 page title.</param>
/// <param name="Description">Resolved UTF-8 summary text (empty when none found).</param>
/// <param name="ImageOverride">Explicit UTF-8 image URL or path from frontmatter (empty when none specified).</param>
/// <param name="CardSlug">Normalized ASCII filename stem for <c>assets/social/&lt;slug&gt;.svg</c>.</param>
[System.Diagnostics.DebuggerDisplay("SocialPageMetadata: {CardSlug}")]
internal readonly record struct SocialPageMetadata(
    byte[] Title,
    byte[] Description,
    byte[] ImageOverride,
    string CardSlug);
