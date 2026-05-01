// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Tags;

/// <summary>One per-page tag occurrence collected during the build.</summary>
/// <param name="Tag">Tag name as it appears in the page's frontmatter.</param>
/// <param name="PageUrl">Output-relative URL of the page that carries the tag (e.g. <c>guide/intro.html</c>).</param>
/// <param name="PageTitle">Resolved page title (h1 text, or the URL when no h1 was rendered).</param>
internal readonly record struct TagEntry(string Tag, string PageUrl, string PageTitle);
