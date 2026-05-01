// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Filesystem layout for generated blog content.
/// </summary>
/// <param name="PostsRoot">Absolute path to the directory holding the post files.</param>
/// <param name="DocsRoot">Absolute path to the docs root.</param>
/// <param name="IndexPath">Absolute path to the generated index markdown file.</param>
/// <param name="IndexTitle">Heading rendered at the top of the generated index page.</param>
/// <param name="EmitArchives">When true, per-tag archive pages are generated.</param>
/// <param name="ArchiveRoot">Absolute directory path where the archive markdown files are written.</param>
/// <param name="ArchiveFallbackSlug">Fallback slug used when a tag contains no slug-safe characters.</param>
public record BlogGenerationOptions(
    string PostsRoot,
    string DocsRoot,
    string IndexPath,
    string IndexTitle,
    bool EmitArchives,
    string ArchiveRoot,
    string ArchiveFallbackSlug)
{
    /// <summary>Validates the option set.</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PostsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(DocsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(IndexPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(IndexTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(ArchiveRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(ArchiveFallbackSlug);
    }
}
