// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
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

    /// <summary>Maps a source-relative path (e.g. <c>guide/intro.md</c>) to a URL path (e.g. <c>guide/intro.html</c>).</summary>
    /// <param name="relativePath">Source path relative to the docs root.</param>
    /// <returns>URL-shaped path; empty when input is unusable.</returns>
    public static string RelativePathToUrlPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return string.Empty;
        }

        var span = relativePath.AsSpan();
        var endsWithMd = span.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var stem = endsWithMd ? span[..^MarkdownExtensionLength] : span;

        var sb = new StringBuilder(stem.Length + (endsWithMd ? 5 : 0));
        for (var i = 0; i < stem.Length; i++)
        {
            sb.Append(stem[i] is '\\' ? '/' : stem[i]);
        }

        if (endsWithMd)
        {
            sb.Append(".html");
        }

        return sb.ToString();
    }

    /// <summary>Emits <c>sitemap.xml</c> under <paramref name="outputRoot"/>.</summary>
    /// <param name="outputRoot">Absolute path to the site output directory.</param>
    /// <param name="baseUrl">Site URL (with trailing slash).</param>
    /// <param name="urlPaths">URL paths relative to <paramref name="baseUrl"/>, sorted.</param>
    public static void WriteSitemap(string outputRoot, string baseUrl, string[] urlPaths)
    {
        var sink = new ArrayBufferWriter<byte>(8 * 1024);
        sink.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"u8);
        sink.Write("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n"u8);

        for (var i = 0; i < urlPaths.Length; i++)
        {
            sink.Write("  <url><loc>"u8);
            WriteEscaped(sink, baseUrl);
            WriteEscaped(sink, urlPaths[i]);
            sink.Write("</loc></url>\n"u8);
        }

        sink.Write("</urlset>\n"u8);
        File.WriteAllBytes(Path.Combine(outputRoot, "sitemap.xml"), sink.WrittenSpan);
    }

    /// <summary>Emits <c>robots.txt</c> with an <c>Allow: *</c> rule and a sitemap pointer.</summary>
    /// <param name="outputRoot">Absolute path to the site output directory.</param>
    /// <param name="baseUrl">Site URL (with trailing slash).</param>
    public static void WriteRobots(string outputRoot, string baseUrl)
    {
        var sink = new ArrayBufferWriter<byte>(256);
        sink.Write("User-agent: *\nAllow: /\n\nSitemap: "u8);
        Utf8StringWriter.Write(sink, baseUrl);
        sink.Write("sitemap.xml\n"u8);
        File.WriteAllBytes(Path.Combine(outputRoot, "robots.txt"), sink.WrittenSpan);
    }

    /// <summary>Writes <paramref name="text"/> as UTF-8, expanding the XML-special characters.</summary>
    /// <param name="sink">Sink.</param>
    /// <param name="text">Source string.</param>
    private static void WriteEscaped(ArrayBufferWriter<byte> sink, string text)
    {
        var max = Encoding.UTF8.GetMaxByteCount(text.Length);
        var buffer = max <= 1024 ? stackalloc byte[max] : new byte[max];
        var written = Encoding.UTF8.GetBytes(text, buffer);
        XmlEntityEscaper.WriteEscaped(sink, buffer[..written], XmlEntityEscaper.Mode.Xml);
    }
}
