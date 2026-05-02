// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Sitemap;

/// <summary>
/// Stateless helpers that compose <c>sitemap.xml</c> and
/// <c>robots.txt</c> bytes and write them to disk under the build
/// output root.
/// </summary>
internal static class SitemapWriter
{
    /// <summary>Length of the <c>.md</c> source extension stripped before composing URL paths.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Length of the <c>.html</c> replacement extension.</summary>
    private const int HtmlExtensionLength = 5;

    /// <summary>Maps a source-relative path (e.g. <c>guide/intro.md</c>) to UTF-8 URL bytes (e.g. <c>guide/intro.html</c>).</summary>
    /// <param name="relativePath">Source path relative to the docs root.</param>
    /// <returns>UTF-8 URL bytes; an empty array when input is unusable.</returns>
    public static byte[] RelativePathToUrlPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return [];
        }

        var span = relativePath.AsSpan();
        var endsWithMd = span.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var keepLength = endsWithMd ? span.Length - MarkdownExtensionLength : span.Length;
        var totalLength = keepLength + (endsWithMd ? HtmlExtensionLength : 0);
        var dst = new byte[totalLength];
        for (var i = 0; i < keepLength; i++)
        {
            var c = span[i];
            dst[i] = c is '\\' ? (byte)'/' : (byte)c;
        }

        if (endsWithMd)
        {
            ".html"u8.CopyTo(dst.AsSpan(keepLength));
        }

        return dst;
    }

    /// <summary>Emits <c>sitemap.xml</c> under <paramref name="outputRoot"/>.</summary>
    /// <param name="outputRoot">Absolute path to the site output directory.</param>
    /// <param name="baseUrl">Site URL (with trailing slash), UTF-8 bytes.</param>
    /// <param name="urlPaths">UTF-8 URL byte paths relative to <paramref name="baseUrl"/>, sorted.</param>
    public static void WriteSitemap(string outputRoot, byte[] baseUrl, byte[][] urlPaths)
    {
        var sink = new ArrayBufferWriter<byte>(8 * 1024);
        sink.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"u8);
        sink.Write("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n"u8);

        for (var i = 0; i < urlPaths.Length; i++)
        {
            sink.Write("  <url><loc>"u8);
            XmlEntityEscaper.WriteEscaped(sink, baseUrl, XmlEntityEscaper.Mode.Xml);
            XmlEntityEscaper.WriteEscaped(sink, urlPaths[i], XmlEntityEscaper.Mode.Xml);
            sink.Write("</loc></url>\n"u8);
        }

        sink.Write("</urlset>\n"u8);
        File.WriteAllBytes(Path.Combine(outputRoot, "sitemap.xml"), sink.WrittenSpan);
    }

    /// <summary>Emits <c>robots.txt</c> with an <c>Allow: *</c> rule and a sitemap pointer.</summary>
    /// <param name="outputRoot">Absolute path to the site output directory.</param>
    /// <param name="baseUrl">Site URL (with trailing slash), UTF-8 bytes.</param>
    public static void WriteRobots(string outputRoot, byte[] baseUrl)
    {
        var sink = new ArrayBufferWriter<byte>(256);
        sink.Write("User-agent: *\nAllow: /\n\nSitemap: "u8);
        sink.Write(baseUrl);
        sink.Write("sitemap.xml\n"u8);
        File.WriteAllBytes(Path.Combine(outputRoot, "robots.txt"), sink.WrittenSpan);
    }
}
