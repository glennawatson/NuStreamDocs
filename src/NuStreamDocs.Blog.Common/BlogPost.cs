// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Parsed metadata for one blog post.
/// </summary>
/// <param name="RelativePath">Source-relative path of the markdown file (forward-slashed).</param>
/// <param name="Slug">URL-safe slug derived from the filename (after the date prefix).</param>
/// <param name="Title">Post title from <c>Title:</c> frontmatter.</param>
/// <param name="Author">Optional author name from <c>Author:</c> frontmatter.</param>
/// <param name="Published">Publication date from <c>Published:</c> frontmatter, or <see cref="DateOnly.MinValue"/> when absent.</param>
/// <param name="Tags">Tags pulled from <c>Tags:</c> frontmatter (single string or comma/space separated).</param>
/// <param name="Excerpt">Plain-text excerpt — first paragraph of the body, used for index listings.</param>
public sealed record BlogPost(
    string RelativePath,
    string Slug,
    string Title,
    string Author,
    DateOnly Published,
    string[] Tags,
    string Excerpt);
