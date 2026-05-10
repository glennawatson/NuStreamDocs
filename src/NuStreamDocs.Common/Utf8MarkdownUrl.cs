// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Converts a source-relative markdown path (e.g. <c>guide\intro.md</c>) to the UTF-8 URL bytes
/// the build pipeline emits for that page (<c>guide/intro.html</c> in flat-URL mode,
/// <c>guide/intro/</c> in directory-URL mode; <c>index.md</c> collapses to its directory under
/// directory-URL mode). Backslashes always normalize to forward slashes.
/// </summary>
public static class Utf8MarkdownUrl
{
    /// <summary>Length of the source <c>.md</c> extension stripped before composing the URL.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Gets the UTF-8 bytes of the <c>.html</c> extension appended in flat-URL mode.</summary>
    private static ReadOnlySpan<byte> HtmlExtension => ".html"u8;

    /// <summary>Gets the UTF-8 bytes of the trailing slash appended in directory-URL mode.</summary>
    private static ReadOnlySpan<byte> TrailingSlash => "/"u8;

    /// <summary>Gets the UTF-8 bytes of the self-directory marker (<c>./</c>) emitted when a bare <c>index.md</c> resolves to its containing directory.</summary>
    private static ReadOnlySpan<byte> SelfDirectory => "./"u8;

    /// <summary>
    /// Maps <paramref name="relativePath"/> to its rendered-page URL.
    /// </summary>
    /// <param name="relativePath">Source path relative to the docs root.</param>
    /// <param name="useDirectoryUrls">True for directory-URL mode (<c>foo/bar/</c>); false for flat-URL mode (<c>foo/bar.html</c>).</param>
    /// <returns>UTF-8 URL bytes; an empty array when <paramref name="relativePath"/> is empty.</returns>
    public static byte[] FromRelativePath(FilePath relativePath, bool useDirectoryUrls)
    {
        if (relativePath.IsEmpty)
        {
            return [];
        }

        var span = relativePath.Value.AsSpan();
        var endsWithMd = span.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        if (!endsWithMd)
        {
            return CopyWithForwardSlashes(span, default);
        }

        var stem = span[..^MarkdownExtensionLength];
        if (!useDirectoryUrls)
        {
            return CopyWithForwardSlashes(stem, HtmlExtension);
        }

        var lastSep = stem.LastIndexOfAny('/', '\\');
        var fileName = lastSep < 0 ? stem : stem[(lastSep + 1)..];
        if (!fileName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            return CopyWithForwardSlashes(stem, TrailingSlash);
        }

        return lastSep < 0
            ? [.. SelfDirectory]
            : CopyWithForwardSlashes(stem[..(lastSep + 1)], default);
    }

    /// <summary>Copies <paramref name="path"/> as UTF-8 with <c>\</c> rewritten to <c>/</c>, then appends <paramref name="suffix"/>.</summary>
    /// <param name="path">Source path span.</param>
    /// <param name="suffix">UTF-8 suffix bytes (e.g. <c>.html</c> or <c>/</c>); empty for no suffix.</param>
    /// <returns>UTF-8 forward-slashed bytes followed by <paramref name="suffix"/>.</returns>
    private static byte[] CopyWithForwardSlashes(ReadOnlySpan<char> path, ReadOnlySpan<byte> suffix)
    {
        var dst = new byte[path.Length + suffix.Length];
        for (var i = 0; i < path.Length; i++)
        {
            var c = path[i];
            dst[i] = c is '\\' ? (byte)'/' : (byte)c;
        }

        suffix.CopyTo(dst.AsSpan(path.Length));
        return dst;
    }
}
