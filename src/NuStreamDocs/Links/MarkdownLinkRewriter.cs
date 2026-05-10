// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Links;

/// <summary>
/// Stateless rendered-HTML rewriter that swaps <c>.md</c> targets
/// in <c>&lt;a href="…"&gt;</c> and <c>&lt;img src="…"&gt;</c>
/// attributes for the <c>.html</c> filename the build pipeline writes,
/// and prepends <c>../</c> to non-<c>.md</c> relative URLs on directory-URL
/// non-index pages so file-relative assets continue to resolve. External
/// URLs (anything with a scheme, server-root, or protocol-relative shape)
/// are left untouched.
/// </summary>
internal static class MarkdownLinkRewriter
{
    /// <summary>ASCII offset to convert an upper-case letter to lower-case.</summary>
    private const int AsciiUpperToLowerOffset = 32;

    /// <summary>Recognized absolute-URL prefixes; matches in <see cref="StartsWithAbsoluteScheme"/> trigger pass-through.</summary>
    private static readonly byte[][] AbsoluteSchemes =
    [
        [.. "http://"u8],
        [.. "https://"u8],
        [.. "ftp://"u8],
        [.. "ftps://"u8],
        [.. "mailto:"u8],
        [.. "tel:"u8],
        [.. "//"u8]
    ];

    /// <summary>Gets the UTF-8 bytes the plugin scans for to short-circuit pages without href attributes.</summary>
    private static ReadOnlySpan<byte> HrefStub => "href=\""u8;

    /// <summary>Gets the UTF-8 bytes the plugin scans for to find image / asset src attributes.</summary>
    private static ReadOnlySpan<byte> SrcStub => "src=\""u8;

    /// <summary>Gets the source-extension suffix (with the leading dot) we replace.</summary>
    private static ReadOnlySpan<byte> MarkdownExtension => ".md"u8;

    /// <summary>Gets the autorefs marker prefix; URLs starting with this are placeholders, not real paths.</summary>
    private static ReadOnlySpan<byte> AutorefMarker => "@autoref:"u8;

    /// <summary>Gets the replacement extension written into the rewritten href.</summary>
    private static ReadOnlySpan<byte> HtmlExtension => ".html"u8;

    /// <summary>Returns true when <paramref name="html"/> contains at least one href or src candidate.</summary>
    /// <param name="html">Rendered HTML.</param>
    /// <returns>True when either prefix is present.</returns>
    public static bool NeedsRewrite(ReadOnlySpan<byte> html) =>
        html.IndexOf(HrefStub) >= 0 || html.IndexOf(SrcStub) >= 0;

    /// <summary>Rewrites every relative <c>.md</c> href in <paramref name="html"/> to <c>.html</c> (flat URL form).</summary>
    /// <param name="html">Rendered HTML.</param>
    /// <returns>The rewritten bytes (or a copy of the original when nothing matched).</returns>
    public static byte[] Rewrite(ReadOnlySpan<byte> html) => Rewrite(html, useDirectoryUrls: false);

    /// <summary>Rewrites every relative <c>.md</c> href in <paramref name="html"/>, picking the URL shape via <paramref name="useDirectoryUrls"/>.</summary>
    /// <param name="html">Rendered HTML.</param>
    /// <param name="useDirectoryUrls">When true, <c>foo.md</c> → <c>foo/</c> (and <c>index.md</c> → empty); when false, <c>foo.md</c> → <c>foo.html</c>.</param>
    /// <returns>The rewritten bytes (or a copy of the original when nothing matched).</returns>
    public static byte[] Rewrite(ReadOnlySpan<byte> html, bool useDirectoryUrls)
    {
        if (html.IsEmpty)
        {
            return [];
        }

        using var rental = PageBuilderPool.Rent(html.Length);
        RewriteInto(html, useDirectoryUrls, rental.Writer);
        return [.. rental.Writer.WrittenSpan];
    }

    /// <summary>Streams the rewrite of <paramref name="html"/> into <paramref name="writer"/>; lets callers feed a pooled writer instead of materializing a fresh <see cref="byte"/> array.</summary>
    /// <param name="html">Rendered HTML.</param>
    /// <param name="useDirectoryUrls">When true, <c>foo.md</c> → <c>foo/</c> (and <c>index.md</c> → empty); when false, <c>foo.md</c> → <c>foo.html</c>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RewriteInto(ReadOnlySpan<byte> html, bool useDirectoryUrls, IBufferWriter<byte> writer) =>
        RewriteInto(html, useDirectoryUrls, prependParent: false, writer);

    /// <summary>
    /// Streams the rewrite of <paramref name="html"/> into <paramref name="writer"/>, optionally prepending
    /// <c>../</c> to every relative href so file-relative links resolve correctly under directory URLs.
    /// </summary>
    /// <param name="html">Rendered HTML.</param>
    /// <param name="useDirectoryUrls">When true, <c>foo.md</c> → <c>foo/</c>.</param>
    /// <param name="prependParent">Prepend <c>../</c> for non-index pages emitted under directory URLs.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void RewriteInto(ReadOnlySpan<byte> html, bool useDirectoryUrls, bool prependParent, IBufferWriter<byte> writer)
    {
        if (html.IsEmpty)
        {
            return;
        }

        var cursor = 0;
        while (cursor < html.Length)
        {
            var hrefRel = html[cursor..].IndexOf(HrefStub);
            var srcRel = html[cursor..].IndexOf(SrcStub);
            if (hrefRel < 0 && srcRel < 0)
            {
                writer.Write(html[cursor..]);
                break;
            }

            // Pick whichever attribute appears first; treat -1 as +infinity so the other side wins.
            var preferHref = hrefRel >= 0 && (srcRel < 0 || hrefRel < srcRel);
            var rel = preferHref ? hrefRel : srcRel;
            var stubLength = preferHref ? HrefStub.Length : SrcStub.Length;

            var attrStart = cursor + rel + stubLength;
            writer.Write(html[cursor..attrStart]);

            var quoteRel = html[attrStart..].IndexOf((byte)'"');
            if (quoteRel < 0)
            {
                writer.Write(html[attrStart..]);
                break;
            }

            var attrEnd = attrStart + quoteRel;
            EmitUrl(html[attrStart..attrEnd], writer, useDirectoryUrls, prependParent);
            cursor = attrEnd;
        }
    }

    /// <summary>Emits a (possibly rewritten) URL attribute value into <paramref name="sink"/>.</summary>
    /// <param name="url">Attribute value bytes (without the surrounding quotes).</param>
    /// <param name="sink">Output sink.</param>
    /// <param name="useDirectoryUrls">Selects the directory-URL output shape.</param>
    /// <param name="prependParent">When true, prepend <c>../</c> for relative paths so file-relative URLs resolve correctly under directory URLs.</param>
    private static void EmitUrl(ReadOnlySpan<byte> url, IBufferWriter<byte> sink, bool useDirectoryUrls, bool prependParent)
    {
        if (!IsRelative(url))
        {
            sink.Write(url);
            return;
        }

        // `@autoref:ID` markers are placeholders that the autorefs PostResolve pass substitutes
        // with the registered absolute URL (e.g. `/api/...`). Prepending `../` here would fuse
        // with the leading `/` after substitution and produce `..//api/...` — the path-construction
        // bug `MalformedLinkDetector.Inspect` flags. Pass markers through untouched.
        if (url.StartsWith(AutorefMarker))
        {
            sink.Write(url);
            return;
        }

        var pathEnd = FindPathEnd(url);
        var path = pathEnd < 0 ? url : url[..pathEnd];
        var tail = pathEnd < 0 ? default : url[pathEnd..];
        if (!path.EndsWith(MarkdownExtension))
        {
            EmitNonMarkdownUrl(url, path, sink, useDirectoryUrls, prependParent);
            return;
        }

        var stem = path[..^MarkdownExtension.Length];
        if (useDirectoryUrls)
        {
            if (prependParent)
            {
                sink.Write("../"u8);
            }

            EmitDirectoryStyle(stem, tail, sink);
            return;
        }

        sink.Write(stem);
        sink.Write(HtmlExtension);
        sink.Write(tail);
    }

    /// <summary>
    /// Writes a non-markdown relative href (image, CSS, asset, plain link), compensating for the
    /// extra directory level that directory-URL non-index pages introduce. Skips the prepend when
    /// the author already wrote a <c>../</c> prefix (mkdocs-relative authoring).
    /// </summary>
    /// <param name="url">Full attribute value including any tail.</param>
    /// <param name="path">Path slice (no <c>?</c>/<c>#</c>).</param>
    /// <param name="sink">Output sink.</param>
    /// <param name="useDirectoryUrls">Directory-URL mode flag.</param>
    /// <param name="prependParent">True when the source page is a non-index directory-URL page.</param>
    private static void EmitNonMarkdownUrl(ReadOnlySpan<byte> url, ReadOnlySpan<byte> path, IBufferWriter<byte> sink, bool useDirectoryUrls, bool prependParent)
    {
        if (prependParent && useDirectoryUrls && !path.StartsWith("../"u8))
        {
            sink.Write("../"u8);
        }

        sink.Write(url);
    }

    /// <summary>Emits the directory-URL form (<c>foo.md</c> → <c>foo/</c>, <c>index.md</c> → directory root).</summary>
    /// <param name="stem">Path stem with the <c>.md</c> extension already removed.</param>
    /// <param name="tail">Anchor / query suffix (including the leading <c>#</c> or <c>?</c>) or empty.</param>
    /// <param name="sink">Output sink.</param>
    private static void EmitDirectoryStyle(ReadOnlySpan<byte> stem, ReadOnlySpan<byte> tail, IBufferWriter<byte> sink)
    {
        var lastSlash = stem.LastIndexOfAny((byte)'/', (byte)'\\');
        var fileName = lastSlash < 0 ? stem : stem[(lastSlash + 1)..];
        if (IsIndex(fileName))
        {
            // index.md → "" (directory root). Preserve the directory prefix when present.
            if (lastSlash >= 0)
            {
                sink.Write(stem[..(lastSlash + 1)]);
            }

            sink.Write(tail);
            return;
        }

        sink.Write(stem);
        sink.Write("/"u8);
        sink.Write(tail);
    }

    /// <summary>Returns true when <paramref name="fileName"/> is the canonical <c>index</c> page name (case-insensitive).</summary>
    /// <param name="fileName">Path-final stem (no extension).</param>
    /// <returns>True for <c>index</c>.</returns>
    private static bool IsIndex(ReadOnlySpan<byte> fileName)
    {
        var needle = "index"u8;
        if (fileName.Length != needle.Length)
        {
            return false;
        }

        for (var i = 0; i < needle.Length; i++)
        {
            var actual = fileName[i] is >= (byte)'A' and <= (byte)'Z' ? (byte)(fileName[i] + AsciiUpperToLowerOffset) : fileName[i];
            if (actual != needle[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns true when <paramref name="href"/> is a same-site relative path eligible for rewriting.</summary>
    /// <param name="href">Attribute value bytes.</param>
    /// <returns>True when the href is relative.</returns>
    private static bool IsRelative(ReadOnlySpan<byte> href) =>
        !href.IsEmpty
        && href[0] is not ((byte)'/' or (byte)'#' or (byte)'?')
        && !StartsWithAbsoluteScheme(href);

    /// <summary>Returns true when <paramref name="href"/> begins with a known absolute-URL scheme.</summary>
    /// <param name="href">Attribute value bytes.</param>
    /// <returns>True when the href is absolute or scheme-prefixed.</returns>
    private static bool StartsWithAbsoluteScheme(ReadOnlySpan<byte> href)
    {
        for (var i = 0; i < AbsoluteSchemes.Length; i++)
        {
            if (href.StartsWith(AbsoluteSchemes[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the offset of the first <c>#</c> or <c>?</c> in <paramref name="href"/>, whichever comes first.</summary>
    /// <param name="href">Attribute value bytes.</param>
    /// <returns>The offset, or -1 when neither is present.</returns>
    private static int FindPathEnd(ReadOnlySpan<byte> href)
    {
        var anchor = href.IndexOf((byte)'#');
        var query = href.IndexOf((byte)'?');
        if (anchor < 0)
        {
            return query;
        }

        return query < 0 || anchor < query ? anchor : query;
    }
}
