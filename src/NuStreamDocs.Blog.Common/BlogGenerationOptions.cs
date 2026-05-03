// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

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
    DirectoryPath PostsRoot,
    DirectoryPath DocsRoot,
    FilePath IndexPath,
    string IndexTitle,
    bool EmitArchives,
    DirectoryPath ArchiveRoot,
    string ArchiveFallbackSlug)
{
    /// <summary>Validates the option set.</summary>
    public void Validate()
    {
        if (PostsRoot.IsEmpty)
        {
            throw new ArgumentException("PostsRoot must be non-empty.", nameof(PostsRoot));
        }

        if (DocsRoot.IsEmpty)
        {
            throw new ArgumentException("DocsRoot must be non-empty.", nameof(DocsRoot));
        }

        if (IndexPath.IsEmpty)
        {
            throw new ArgumentException("IndexPath must be non-empty.", nameof(IndexPath));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(IndexTitle);

        if (ArchiveRoot.IsEmpty)
        {
            throw new ArgumentException("ArchiveRoot must be non-empty.", nameof(ArchiveRoot));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ArchiveFallbackSlug);
    }
}
