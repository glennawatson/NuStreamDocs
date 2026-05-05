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
/// <param name="IndexTitle">UTF-8 heading bytes rendered at the top of the generated index page.</param>
/// <param name="EmitArchives">When true, per-tag archive pages are generated.</param>
/// <param name="ArchiveRoot">Absolute directory path where the archive markdown files are written.</param>
/// <param name="ArchiveFallbackSlug">UTF-8 fallback slug bytes used when a tag contains no slug-safe characters.</param>
public record BlogGenerationOptions(
    DirectoryPath PostsRoot,
    DirectoryPath DocsRoot,
    FilePath IndexPath,
    byte[] IndexTitle,
    bool EmitArchives,
    DirectoryPath ArchiveRoot,
    byte[] ArchiveFallbackSlug)
{
    /// <summary>Validates the option set.</summary>
    public void Validate()
    {
        ThrowIfEmptyDirectory(PostsRoot, nameof(PostsRoot));
        ThrowIfEmptyDirectory(DocsRoot, nameof(DocsRoot));
        ThrowIfEmptyFile(IndexPath, nameof(IndexPath));
        ThrowIfEmptyBytes(IndexTitle, nameof(IndexTitle));
        ThrowIfEmptyDirectory(ArchiveRoot, nameof(ArchiveRoot));
        ThrowIfEmptyBytes(ArchiveFallbackSlug, nameof(ArchiveFallbackSlug));
    }

    /// <summary>Throws when <paramref name="value"/> is null or zero-length.</summary>
    /// <param name="value">Bytes to validate.</param>
    /// <param name="paramName">Name carried into the exception.</param>
    private static void ThrowIfEmptyBytes(byte[]? value, string paramName)
    {
        if (value is [_, ..])
        {
            return;
        }

        throw new ArgumentException($"{paramName} bytes must be non-empty.", paramName);
    }

    /// <summary>Throws when <paramref name="value"/> is empty.</summary>
    /// <param name="value">Directory path to validate.</param>
    /// <param name="paramName">Name carried into the exception.</param>
    private static void ThrowIfEmptyDirectory(DirectoryPath value, string paramName)
    {
        if (!value.IsEmpty)
        {
            return;
        }

        throw new ArgumentException($"{paramName} must be non-empty.", paramName);
    }

    /// <summary>Throws when <paramref name="value"/> is empty.</summary>
    /// <param name="value">File path to validate.</param>
    /// <param name="paramName">Name carried into the exception.</param>
    private static void ThrowIfEmptyFile(FilePath value, string paramName)
    {
        if (!value.IsEmpty)
        {
            return;
        }

        throw new ArgumentException($"{paramName} must be non-empty.", paramName);
    }
}
