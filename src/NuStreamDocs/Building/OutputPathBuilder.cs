// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Building;

/// <summary>Maps a source-relative <c>.md</c> path to an absolute output path under the build's output root.</summary>
/// <remarks>
/// Two variants are exposed: flat URLs (<c>guide/foo.md</c> → <c>guide/foo.html</c>) and directory URLs
/// (<c>guide/foo.md</c> → <c>guide/foo/index.html</c>; <c>index.md</c> stays flat).
/// </remarks>
internal static class OutputPathBuilder
{
    /// <summary>Length of the source <c>.md</c> extension stripped before composing the output path.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Length of the replacement <c>.html</c> extension.</summary>
    private const int HtmlExtensionLength = 5;

    /// <summary>Length of the trailing <c>/index.html</c> appended to directory-URL outputs (separator + 'index.html').</summary>
    private const int IndexHtmlSuffixLength = 11;

    /// <summary>Source extension recognized by the path mapper.</summary>
    private const string MarkdownExtension = ".md";

    /// <summary>Replacement extension written into the output path.</summary>
    private const string HtmlExtension = ".html";

    /// <summary>Trailing <c>index.html</c> file name (without leading separator).</summary>
    private const string IndexHtml = "index.html";

    /// <summary>Flat-URL form: <c>foo.md</c> → <c>foo.html</c>; everything else passes through.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="relativePath">Source-relative path.</param>
    /// <returns>The absolute output path.</returns>
    public static string ForFlatUrls(string outputRoot, string relativePath)
    {
        var relSpan = relativePath.AsSpan();
        var endsWithMd = relSpan.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase);
        var keepLength = endsWithMd ? relSpan.Length - MarkdownExtensionLength : relSpan.Length;
        var totalLength = outputRoot.Length + 1 + keepLength + (endsWithMd ? HtmlExtensionLength : 0);
        var separator = Path.DirectorySeparatorChar;

        return string.Create(
            totalLength,
            (outputRoot, relativePath, keepLength, endsWithMd, separator),
            static (span, state) => WriteOutputPath(span, state));
    }

    /// <summary>Directory-URL form: <c>guide/foo.md</c> → <c>guide/foo/index.html</c>; <c>guide/index.md</c> stays as <c>guide/index.html</c>.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="relativePath">Source-relative path.</param>
    /// <returns>The absolute output path.</returns>
    public static string ForDirectoryUrls(string outputRoot, string relativePath)
    {
        var relSpan = relativePath.AsSpan();
        if (!relSpan.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase))
        {
            return ForFlatUrls(outputRoot, relativePath);
        }

        var stem = relSpan[..^MarkdownExtensionLength];
        var lastSep = stem.LastIndexOfAny('/', '\\');
        var fileName = lastSep < 0 ? stem : stem[(lastSep + 1)..];
        if (fileName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            return ForFlatUrls(outputRoot, relativePath);
        }

        var stemLength = stem.Length;
        var totalLength = outputRoot.Length + 1 + stemLength + IndexHtmlSuffixLength;
        var separator = Path.DirectorySeparatorChar;
        return string.Create(
            totalLength,
            (outputRoot, relativePath, stemLength, separator),
            static (span, state) => WriteDirectoryUrlPath(span, state));
    }

    /// <summary>Writes the flat-URL output path bytes into <paramref name="span"/>.</summary>
    /// <param name="span">Pre-sized destination span.</param>
    /// <param name="state">Tuple of (outputRoot, relativePath, keepLength, endsWithMd, separator).</param>
    private static void WriteOutputPath(in Span<char> span, (string Root, string Rel, int Keep, bool Swap, char Sep) state)
    {
        state.Root.AsSpan().CopyTo(span);
        span[state.Root.Length] = state.Sep;
        var tail = span[(state.Root.Length + 1)..];
        state.Rel.AsSpan(0, state.Keep).CopyTo(tail);
        if (!state.Swap)
        {
            return;
        }

        HtmlExtension.AsSpan().CopyTo(tail[state.Keep..]);
    }

    /// <summary>Writes a directory-URL output path (<c>outputRoot/sep/stem/sep/index.html</c>) into <paramref name="span"/>.</summary>
    /// <param name="span">Pre-sized destination span.</param>
    /// <param name="state">Tuple of (outputRoot, relativePath, stemLength, separator).</param>
    private static void WriteDirectoryUrlPath(in Span<char> span, (string Root, string Rel, int StemLen, char Sep) state)
    {
        state.Root.AsSpan().CopyTo(span);
        span[state.Root.Length] = state.Sep;
        var afterRoot = span[(state.Root.Length + 1)..];
        state.Rel.AsSpan(0, state.StemLen).CopyTo(afterRoot);
        afterRoot[state.StemLen] = state.Sep;
        IndexHtml.AsSpan().CopyTo(afterRoot[(state.StemLen + 1)..]);
    }
}
