// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Parsed metadata for one blog post.
/// </summary>
/// <param name="RelativePath">Source-relative path of the markdown file (forward-slashed) — kept as <see cref="FilePath"/> because every consumer hands it to file IO.</param>
/// <param name="RelativeUrlUtf8">Docs-root-relative <c>.md</c> path bytes; emitted inside <c>[Title](...)</c> so the link rewriter picks the build's URL form.</param>
/// <param name="Slug">URL-safe slug derived from the filename (after the date prefix), as UTF-8 bytes.</param>
/// <param name="Title">Post title from <c>Title:</c> frontmatter, as UTF-8 bytes.</param>
/// <param name="Lead">Optional lead-in paragraph from <c>Lead:</c> frontmatter, as UTF-8 bytes; empty when absent.</param>
/// <param name="Description">Optional description from <c>Description:</c> frontmatter, as UTF-8 bytes; empty when absent.</param>
/// <param name="Author">Optional author name from <c>Author:</c> frontmatter, as UTF-8 bytes; empty when absent.</param>
/// <param name="Published">Publication date from <c>Published:</c> frontmatter, or <see cref="DateOnly.MinValue"/> when absent.</param>
/// <param name="Tags">Tags pulled from <c>Tags:</c> frontmatter (single value or comma/space separated), each as UTF-8 bytes.</param>
/// <param name="Excerpt">Plain-text excerpt — first paragraph of the body, used for index listings; empty when absent.</param>
public sealed record BlogPost(
    FilePath RelativePath,
    byte[] RelativeUrlUtf8,
    byte[] Slug,
    byte[] Title,
    byte[] Lead,
    byte[] Description,
    byte[] Author,
    DateOnly Published,
    byte[][] Tags,
    byte[] Excerpt);
