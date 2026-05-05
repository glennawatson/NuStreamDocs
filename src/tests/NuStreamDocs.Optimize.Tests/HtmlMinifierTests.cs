// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Optimize.Tests;

/// <summary>Behavior tests for <c>HtmlMinifier</c>.</summary>
public class HtmlMinifierTests
{
    /// <summary>Inter-tag whitespace runs collapse to a single space (or vanish entirely between tags).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CollapsesWhitespaceBetweenTags() => await Assert.That(Minify("<p>  hello   world  </p>")).IsEqualTo("<p>hello world</p>");

    /// <summary>HTML comments are stripped by default.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StripsComments() => await Assert.That(Minify("<p>a<!-- ignore me -->b</p>")).IsEqualTo("<p>ab</p>");

    /// <summary>Whitespace inside <c>&lt;pre&gt;</c> is preserved verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreservesPreContent()
    {
        const string Input = "<pre>  line1\n    line2\n</pre>";
        await Assert.That(Minify(Input)).IsEqualTo(Input);
    }

    /// <summary>Whitespace and comment-like markers inside <c>&lt;script&gt;</c> are preserved.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreservesScriptContent()
    {
        const string Input = "<script>  var x = 1; // comment\n  var y = 2;</script>";
        await Assert.That(Minify(Input)).IsEqualTo(Input);
    }

    /// <summary>Tag attributes survive the rewrite intact.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreservesTagAttributes() => await Assert.That(Minify("<a href=\"x\"  class=\"y\" >link</a>")).IsEqualTo("<a href=\"x\"  class=\"y\" >link</a>");

    /// <summary>Whitespace between block-level tags collapses to nothing (no leading space after a tag).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DropsWhitespaceBetweenBlockTags() => await Assert.That(Minify("<p>a</p>\n\n  <p>b</p>")).IsEqualTo("<p>a</p><p>b</p>");

    /// <summary>Empty input produces empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput() => await Assert.That(Minify(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Disabling whitespace collapse passes text through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RespectsCollapseDisabledOption()
    {
        HtmlMinifyOptions options = new(StripComments: true, CollapseWhitespace: false);
        await Assert.That(Minify("<p>  a   b  </p>", options)).IsEqualTo("<p>  a   b  </p>");
    }

    /// <summary>Disabling comment-strip leaves comments intact.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RespectsKeepCommentsOption()
    {
        HtmlMinifyOptions options = new(StripComments: false, CollapseWhitespace: true);
        await Assert.That(Minify("<p>a<!-- keep -->b</p>", options)).IsEqualTo("<p>a<!-- keep -->b</p>");
    }

    /// <summary>Drives the minifier with default options.</summary>
    /// <param name="html">Source HTML string.</param>
    /// <returns>Minified HTML string.</returns>
    private static string Minify(string html) => Minify(html, HtmlMinifyOptions.Default);

    /// <summary>Drives the minifier with the supplied options.</summary>
    /// <param name="html">Source HTML string.</param>
    /// <param name="options">Options.</param>
    /// <returns>Minified HTML string.</returns>
    private static string Minify(string html, HtmlMinifyOptions options)
    {
        ArrayBufferWriter<byte> sink = new(Math.Max(1, html.Length));
        HtmlMinifier.Minify(Encoding.UTF8.GetBytes(html), sink, options);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
