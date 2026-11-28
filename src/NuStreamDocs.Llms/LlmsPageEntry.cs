// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Llms;

/// <summary>Captured per-page metadata and clean Markdown body for <c>llms.txt</c> and <c>llms-full.txt</c> emission.</summary>
/// <param name="Section">UTF-8 section heading (for example <c>Guide</c> or <c>Overview</c>).</param>
/// <param name="UrlPath">UTF-8 relative URL path (for example <c>guide/intro.html</c> or <c>guide/intro/</c>).</param>
/// <param name="RelativePath">Page path relative to the docs root for optional <c>.md</c> sibling output.</param>
/// <param name="Title">UTF-8 page title.</param>
/// <param name="Summary">UTF-8 short page summary (may be empty).</param>
/// <param name="BodyMarkdown">UTF-8 Markdown body with YAML front matter removed.</param>
[System.Diagnostics.DebuggerDisplay("LlmsPageEntry: {RelativePath}")]
internal readonly record struct LlmsPageEntry(
    byte[] Section,
    byte[] UrlPath,
    FilePath RelativePath,
    byte[] Title,
    byte[] Summary,
    byte[] BodyMarkdown);
