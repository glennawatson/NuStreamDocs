// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Playgrounds;

/// <summary>Emits accessible CSS-tabbed HTTP client snippets (<c>curl</c>, C# <c>HttpClient</c>, JavaScript <c>fetch</c>) plus an optional Try-It drawer.</summary>
internal static class HttpRequestWidgetEmitter
{
    /// <summary>Buffer size for integer tab group formatting.</summary>
    private const int IntBufferLength = 12;

    /// <summary>Template tokens for cURL command snippets.</summary>
    private static readonly SnippetSyntax CurlSyntax = new(
        new([.. "curl -X "u8], [.. " \""u8], [.. "\""u8], [.. " \\\n  -H \""u8], [.. ": "u8], [.. "\""u8]),
        new([.. " \\\n  -d '"u8], [.. "'"u8], []));

    /// <summary>Template tokens for C# <c>HttpClient</c> snippets.</summary>
    private static readonly SnippetSyntax CSharpSyntax = new(
        new(
            [.. "using var request = new HttpRequestMessage(new HttpMethod(\""u8],
            [.. "\"), \""u8],
            [.. "\");\n"u8],
            [.. "request.Headers.TryAddWithoutValidation(\""u8],
            [.. "\", \""u8],
            [.. "\");\n"u8]),
        new(
            [.. "request.Content = new StringContent("u8],
            [.. ");\n"u8],
            [.. "using var response = await httpClient.SendAsync(request);"u8]));

    /// <summary>Monotonic counter ensuring radio tab group names are unique across a build.</summary>
    private static int _nextWidgetId;

    /// <summary>Gets the closing <c>&lt;/code&gt;&lt;/pre&gt;&lt;/div&gt;</c> sequence for each snippet panel.</summary>
    private static ReadOnlySpan<byte> PanelClose => "</code></pre></div>"u8;

    /// <summary>Renders an <c>http-request</c> block into <paramref name="writer"/>.</summary>
    /// <param name="content">Decoded UTF-8 fence payload.</param>
    /// <param name="enableTryIt">Whether to append the interactive Try-It drawer.</param>
    /// <param name="writer">Destination buffer.</param>
    internal static void Render(ReadOnlySpan<byte> content, bool enableTryIt, IBufferWriter<byte> writer)
    {
        var spec = HttpRequestParser.Parse(content);
        var widgetId = Interlocked.Increment(ref _nextWidgetId);

        writer.Write("<div class=\"nd-http-playground\" data-method=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, spec.Method, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\" data-url=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, spec.Url, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\"><div class=\"nd-http-bar\"><span class=\"nd-http-method\">"u8);
        XmlEntityEscaper.WriteEscaped(writer, spec.Method, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("</span><code class=\"nd-http-url\">"u8);
        XmlEntityEscaper.WriteEscaped(writer, spec.Url, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("</code></div><div class=\"nd-http-tabs\">"u8);

        WriteTabHeader(writer, widgetId, "curl"u8, "cURL"u8, true);
        WriteTemplatedPanel(writer, spec, CurlSyntax);

        WriteTabHeader(writer, widgetId, "csharp"u8, "C#"u8, false);
        WriteTemplatedPanel(writer, spec, CSharpSyntax);

        WriteTabHeader(writer, widgetId, "fetch"u8, "fetch"u8, false);
        WriteFetchPanel(writer, spec);

        writer.Write("</div>"u8);
        if (enableTryIt)
        {
            WriteTryItDrawer(writer, spec);
        }

        writer.Write("</div>"u8);
    }

    /// <summary>Writes the radio input and label opening a pure-CSS snippet tab.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="widgetId">Unique widget number.</param>
    /// <param name="tabSlug">Tab slug.</param>
    /// <param name="label">Visible tab label.</param>
    /// <param name="isDefault">Whether checked initially.</param>
    private static void WriteTabHeader(
        IBufferWriter<byte> writer,
        int widgetId,
        ReadOnlySpan<byte> tabSlug,
        ReadOnlySpan<byte> label,
        bool isDefault)
    {
        Span<byte> idBuf = stackalloc byte[IntBufferLength];
        _ = Utf8Formatter.TryFormat(widgetId, idBuf, out var idLen);
        var idSpan = idBuf[..idLen];

        writer.Write("<input type=\"radio\" class=\"nd-http-tab-input\" name=\"nd-http-grp-"u8);
        writer.Write(idSpan);
        writer.Write("\" id=\"nd-http-"u8);
        writer.Write(idSpan);
        writer.Write("-"u8);
        writer.Write(tabSlug);
        writer.Write(isDefault ? "\" checked><label class=\"nd-http-tab-label\" for=\"nd-http-"u8 : "\"><label class=\"nd-http-tab-label\" for=\"nd-http-"u8);
        writer.Write(idSpan);
        writer.Write("-"u8);
        writer.Write(tabSlug);
        writer.Write("\">"u8);
        writer.Write(label);
        writer.Write("</label><div class=\"nd-http-panel\"><pre><code>"u8);
    }

    /// <summary>Writes a snippet panel parameterized by <paramref name="syntax"/>.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="spec">Parsed HTTP spec.</param>
    /// <param name="syntax">Language delimiter tokens.</param>
    private static void WriteTemplatedPanel(IBufferWriter<byte> writer, in HttpRequestSpec spec, in SnippetSyntax syntax)
    {
        var head = syntax.LeadAndHeaders;
        writer.Write(head.LeadPrefix);
        XmlEntityEscaper.WriteEscaped(writer, spec.Method, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write(head.LeadMiddle);
        XmlEntityEscaper.WriteEscaped(writer, spec.Url, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write(head.LeadSuffix);

        for (var i = 0; i < spec.Headers.Length; i++)
        {
            writer.Write(head.HeaderPrefix);
            XmlEntityEscaper.WriteEscaped(writer, spec.Headers[i].Name, XmlEntityEscaper.Mode.HtmlAttribute);
            writer.Write(head.HeaderSeparator);
            XmlEntityEscaper.WriteEscaped(writer, spec.Headers[i].Value, XmlEntityEscaper.Mode.HtmlAttribute);
            writer.Write(head.HeaderSuffix);
        }

        var tail = syntax.BodyAndTrailer;
        if (spec.Body.Length > 0)
        {
            writer.Write(tail.BodyPrefix);
            XmlEntityEscaper.WriteEscaped(writer, spec.Body, XmlEntityEscaper.Mode.HtmlAttribute);
            writer.Write(tail.BodySuffix);
        }

        writer.Write(tail.Trailer);
        writer.Write(PanelClose);
    }

    /// <summary>Writes the JavaScript <c>fetch</c> snippet panel.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="spec">Parsed HTTP spec.</param>
    private static void WriteFetchPanel(IBufferWriter<byte> writer, in HttpRequestSpec spec)
    {
        writer.Write("const response = await fetch(\""u8);
        XmlEntityEscaper.WriteEscaped(writer, spec.Url, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\", {\n  method: \""u8);
        XmlEntityEscaper.WriteEscaped(writer, spec.Method, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\""u8);
        if (spec.Headers.Length > 0)
        {
            writer.Write(",\n  headers: { "u8);
            for (var i = 0; i < spec.Headers.Length; i++)
            {
                if (i > 0)
                {
                    writer.Write(", "u8);
                }

                writer.Write("\""u8);
                XmlEntityEscaper.WriteEscaped(writer, spec.Headers[i].Name, XmlEntityEscaper.Mode.HtmlAttribute);
                writer.Write("\": \""u8);
                XmlEntityEscaper.WriteEscaped(writer, spec.Headers[i].Value, XmlEntityEscaper.Mode.HtmlAttribute);
                writer.Write("\""u8);
            }

            writer.Write(" }"u8);
        }

        if (spec.Body.Length > 0)
        {
            writer.Write(",\n  body: "u8);
            XmlEntityEscaper.WriteEscaped(writer, spec.Body, XmlEntityEscaper.Mode.HtmlAttribute);
        }

        writer.Write("\n});"u8);
        writer.Write(PanelClose);
    }

    /// <summary>Writes the collapsible interactive Try-It drawer.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="spec">Parsed HTTP spec.</param>
    private static void WriteTryItDrawer(IBufferWriter<byte> writer, in HttpRequestSpec spec)
    {
        writer.Write("<details class=\"nd-http-tryit\"><summary>Try Request</summary><div class=\"nd-http-tryit-body\">"u8);
        if (spec.Body.Length > 0)
        {
            writer.Write("<textarea class=\"nd-http-body-input\" rows=\"4\" aria-label=\"Request body\">"u8);
            XmlEntityEscaper.WriteEscaped(writer, spec.Body, XmlEntityEscaper.Mode.HtmlAttribute);
            writer.Write("</textarea>"u8);
        }

        writer.Write("<button type=\"button\" class=\"nd-http-send\">Send Request</button><pre class=\"nd-http-response\" aria-live=\"polite\"></pre></div></details>"u8);
    }

    /// <summary>Combined request-line, header, and body syntax tokens.</summary>
    /// <param name="LeadAndHeaders">Request-line and header delimiters.</param>
    /// <param name="BodyAndTrailer">Body and trailing statement delimiters.</param>
    private readonly record struct SnippetSyntax(LeadHeaderSyntax LeadAndHeaders, BodyTrailerSyntax BodyAndTrailer);

    /// <summary>Delimiters for the request line and per-header lines.</summary>
    /// <param name="LeadPrefix">Bytes before HTTP method.</param>
    /// <param name="LeadMiddle">Bytes between HTTP method and URL.</param>
    /// <param name="LeadSuffix">Bytes after URL.</param>
    /// <param name="HeaderPrefix">Bytes before header name.</param>
    /// <param name="HeaderSeparator">Bytes between header name and value.</param>
    /// <param name="HeaderSuffix">Bytes after header value.</param>
    private readonly record struct LeadHeaderSyntax(
        byte[] LeadPrefix,
        byte[] LeadMiddle,
        byte[] LeadSuffix,
        byte[] HeaderPrefix,
        byte[] HeaderSeparator,
        byte[] HeaderSuffix);

    /// <summary>Delimiters for the optional request body and trailing call.</summary>
    /// <param name="BodyPrefix">Bytes before request body.</param>
    /// <param name="BodySuffix">Bytes after request body.</param>
    /// <param name="Trailer">Bytes before closing code tags.</param>
    private readonly record struct BodyTrailerSyntax(byte[] BodyPrefix, byte[] BodySuffix, byte[] Trailer);
}
