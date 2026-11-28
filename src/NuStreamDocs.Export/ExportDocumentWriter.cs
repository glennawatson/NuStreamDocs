// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Export;

/// <summary>Assembles the standalone single-file <c>/print.html</c> document from sorted <see cref="ExportPageEntry"/> chapters.</summary>
internal static class ExportDocumentWriter
{
    /// <summary>Initial output buffer capacity.</summary>
    private const int InitialCapacity = 4096;

    /// <summary>Builds the complete UTF-8 HTML document for <paramref name="entries"/>.</summary>
    /// <param name="options">Export configuration.</param>
    /// <param name="entries">Deterministically ordered chapters.</param>
    /// <returns>Standalone UTF-8 HTML bytes.</returns>
    internal static byte[] BuildDocument(ExportOptions options, ReadOnlySpan<ExportPageEntry> entries)
    {
        var routeLookup = ExportLinkRewriter.BuildRouteLookup(entries);
        ArrayBufferWriter<byte> writer = new(InitialCapacity);

        writer.Write("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n<title>"u8);
        XmlEntityEscaper.WriteEscaped(writer, options.DocumentTitle, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("</title>\n<style>\n"u8);
        writer.Write(ExportStylesheet.Bytes);
        writer.Write("\n</style>\n</head>\n<body>\n<header class=\"nd-export-cover\"><h1>"u8);
        XmlEntityEscaper.WriteEscaped(writer, options.DocumentTitle, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("</h1>"u8);
        if (options.Subtitle.Length > 0)
        {
            writer.Write("<p class=\"nd-export-subtitle\">"u8);
            XmlEntityEscaper.WriteEscaped(writer, options.Subtitle, XmlEntityEscaper.Mode.HtmlAttribute);
            writer.Write("</p>"u8);
        }

        writer.Write("</header>\n"u8);

        if (options.IncludeTableOfContents)
        {
            WriteTableOfContents(writer, entries);
        }

        writer.Write("<main class=\"nd-export-chapters\">\n"u8);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            writer.Write("<section class=\"nd-export-chapter\" id=\""u8);
            writer.Write(entry.SectionId);
            writer.Write("\">\n"u8);
            ExportLinkRewriter.RewriteLinks(entry.BodyHtml, routeLookup, writer);
            writer.Write("\n</section>\n"u8);
        }

        writer.Write("</main>\n</body>\n</html>\n"u8);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Writes the consolidated chapter table of contents.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="entries">Sorted chapters.</param>
    private static void WriteTableOfContents(IBufferWriter<byte> writer, ReadOnlySpan<ExportPageEntry> entries)
    {
        writer.Write("<nav class=\"nd-export-toc\" aria-label=\"Table of contents\"><h2>Contents</h2><ol>\n"u8);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            writer.Write("<li><a href=\"#"u8);
            writer.Write(entry.SectionId);
            writer.Write("\">"u8);
            XmlEntityEscaper.WriteEscaped(writer, entry.Title, XmlEntityEscaper.Mode.HtmlAttribute);
            writer.Write("</a></li>\n"u8);
        }

        writer.Write("</ol></nav>\n"u8);
    }
}
