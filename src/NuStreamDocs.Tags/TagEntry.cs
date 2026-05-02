// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Tags;

/// <summary>One per-page tag occurrence collected during the build.</summary>
/// <param name="Tag">UTF-8 tag bytes as they appear in the page's frontmatter.</param>
/// <param name="PageUrl">UTF-8 output-relative URL bytes (e.g. <c>guide/intro.html</c>).</param>
/// <param name="PageTitle">UTF-8 page-title bytes (h1 text, or the URL bytes when no h1 was rendered).</param>
internal readonly record struct TagEntry(byte[] Tag, byte[] PageUrl, byte[] PageTitle);
