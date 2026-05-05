// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Building;

/// <summary>
/// Copies non-markdown content from the docs input root into the site output root —
/// images, vendor JS/CSS, fonts, downloadable files, anything the user dropped into
/// <c>docs/</c> that isn't a page.
/// </summary>
/// <remarks>
/// Pages are handled by the per-page render pipeline; theme/plugin assets ride along
/// via <c>IStaticAssetProvider</c>. This step plugs the third gap — content the
/// site author placed under <c>docs/</c> directly (logo PNGs, favicons, embedded
/// videos, custom JS, etc.) — so links from page templates and markdown resolve
/// against real files in the output tree.
/// </remarks>
internal static class DocsAssetCopier
{
    /// <summary>Markdown extension; pages are emitted by the render pipeline so we skip them here.</summary>
    private const string MarkdownExtension = ".md";

    /// <summary>Nav override file name; consumed by <c>NuStreamDocs.Nav</c>, not user content.</summary>
    private const string PagesFileName = ".pages";

    /// <summary>Walks <paramref name="inputRoot"/> and copies every non-page file to the matching path under <paramref name="outputRoot"/>.</summary>
    /// <param name="inputRoot">Absolute docs input root.</param>
    /// <param name="outputRoot">Absolute site output root.</param>
    /// <param name="filter">Path filter from the build options; honors the same include/exclude globs that govern page discovery.</param>
    /// <returns>Number of files copied.</returns>
    public static int Copy(DirectoryPath inputRoot, DirectoryPath outputRoot, PathFilter filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputRoot.Value);
        ArgumentException.ThrowIfNullOrEmpty(outputRoot.Value);
        ArgumentNullException.ThrowIfNull(filter);

        if (!inputRoot.Exists())
        {
            return 0;
        }

        var copied = 0;
        var rootLength = inputRoot.Value.Length + (inputRoot.Value.EndsWith(Path.DirectorySeparatorChar) ? 0 : 1);
        var sourceFiles = Directory.GetFiles(inputRoot.Value, "*", SearchOption.AllDirectories);
        for (var i = 0; i < sourceFiles.Length; i++)
        {
            var sourcePath = sourceFiles[i];
            if (!ShouldCopy(sourcePath, rootLength, filter, out var relative))
            {
                continue;
            }

            var destPath = Path.Combine(outputRoot.Value, relative.Replace(Path.DirectorySeparatorChar, '/'));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            // Kernel-level copy (CopyFile / copy_file_range) — same primitive the privacy
            // downloader uses for cache → output materialization.
            File.Copy(sourcePath, destPath, overwrite: true);
            copied++;
        }

        return copied;
    }

    /// <summary>Decides whether <paramref name="sourcePath"/> is eligible for copy and emits the input-relative path on success.</summary>
    /// <param name="sourcePath">Absolute file path under <paramref name="rootLength"/>.</param>
    /// <param name="rootLength">Length (including trailing separator) of the docs input root prefix.</param>
    /// <param name="filter">Path filter.</param>
    /// <param name="relative">Forward-slash-able relative path on success.</param>
    /// <returns>True when the file should be copied.</returns>
    private static bool ShouldCopy(string sourcePath, int rootLength, PathFilter filter, out string relative)
    {
        relative = sourcePath[rootLength..];
        if (IsHiddenPath(relative))
        {
            return false;
        }

        var fileName = Path.GetFileName(sourcePath.AsSpan());
        if (fileName.Equals(PagesFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(sourcePath.AsSpan());
        return !extension.Equals(MarkdownExtension, StringComparison.OrdinalIgnoreCase)
            && (!filter.HasRules || filter.Matches(relative.Replace(Path.DirectorySeparatorChar, '/')));
    }

    /// <summary>True when any segment of <paramref name="relative"/> begins with <c>.</c> — skips dotfiles and dot-prefixed directories like <c>.git</c> / <c>.cache</c>.</summary>
    /// <param name="relative">Input-relative path.</param>
    /// <returns>True for hidden paths.</returns>
    private static bool IsHiddenPath(string relative)
    {
        var span = relative.AsSpan();
        var segmentStart = 0;
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] is '/' or '\\')
            {
                if (i > segmentStart && span[segmentStart] is '.')
                {
                    return true;
                }

                segmentStart = i + 1;
            }
        }

        return segmentStart < span.Length && span[segmentStart] is '.';
    }
}
