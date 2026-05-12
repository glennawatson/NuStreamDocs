// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Redirects.Tests;

/// <summary>Coverage for <see cref="RedirectFileWriter"/> and <see cref="HeadersFileWriter"/>.</summary>
public class RedirectFileWriterTests
{
    /// <summary>The <c>_redirects</c> file has one sorted line per rule with the right status code.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RedirectsFileFormat()
    {
        RedirectRule[] rules =
        [
            new([.. "/zeta/"u8], [.. "/new-zeta/"u8], Permanent: true),
            new([.. "/alpha/"u8], [.. "https://other.test/"u8], Permanent: false),
        ];
        ArrayBufferWriter<byte> sink = new();
        RedirectFileWriter.WriteRedirectsFile(rules, sink);
        var text = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(text).IsEqualTo("/alpha/  https://other.test/  302\n/zeta/  /new-zeta/  301\n");
    }

    /// <summary>The meta-refresh page has the refresh meta, a canonical link, and noindex.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MetaRefreshHtml()
    {
        ArrayBufferWriter<byte> sink = new();
        RedirectFileWriter.WriteMetaRefreshHtml("/new/path/"u8, sink);
        var html = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(html).Contains("<meta http-equiv=\"refresh\" content=\"0; url=/new/path/\">");
        await Assert.That(html).Contains("<link rel=\"canonical\" href=\"/new/path/\">");
        await Assert.That(html).Contains("content=\"noindex\"");
        await Assert.That(html).Contains("<a href=\"/new/path/\">/new/path/</a>");
    }

    /// <summary>The default cache rules cover <c>/assets/*</c> then the more-specific <c>/assets/fonts/*</c>; the headers file lays them out as indented blocks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeadersFileFormat()
    {
        var rules = new List<HeaderRule>(HeadersFileWriter.DefaultRules())
        {
            new([.. "/api/*"u8], [[.. "X-Robots-Tag: noindex"u8]]),
        };
        ArrayBufferWriter<byte> sink = new();
        HeadersFileWriter.WriteHeadersFile(rules, sink);
        var text = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(text).Contains("/assets/*\n  Cache-Control: public, max-age=604800\n\n");
        await Assert.That(text).Contains("/assets/fonts/*\n  Cache-Control: public, max-age=31536000, immutable\n\n");
        await Assert.That(text).Contains("/api/*\n  X-Robots-Tag: noindex\n\n");

        // /assets/fonts/* must come after /assets/* so it wins for font files.
        await Assert.That(text.IndexOf("/assets/fonts/*", StringComparison.Ordinal)).IsGreaterThan(text.IndexOf("/assets/*\n", StringComparison.Ordinal));
    }

    /// <summary>An empty rule list produces no <c>_headers</c> output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyHeadersFileIsEmpty()
    {
        ArrayBufferWriter<byte> sink = new();
        HeadersFileWriter.WriteHeadersFile([], sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }
}
