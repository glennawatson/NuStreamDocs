// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Optimize.Tests;

/// <summary>Parameterized HtmlMinifier inputs covering the small-but-many byte-class branches.</summary>
public class HtmlMinifierParameterizedTests
{
    /// <summary>Each preserve tag (case-insensitive) keeps its body verbatim.</summary>
    /// <param name="tag">Tag name to test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("pre")]
    [Arguments("PRE")]
    [Arguments("script")]
    [Arguments("SCRIPT")]
    [Arguments("style")]
    [Arguments("STYLE")]
    [Arguments("textarea")]
    [Arguments("TEXTAREA")]
    public async Task PreserveTagsKeepBody(string tag)
    {
        var input = $"<{tag}>  raw\n  body  </{tag}>";
        await Assert.That(Minify(input)).IsEqualTo(input);
    }

    /// <summary>Whitespace classes inside text runs all collapse to one space.</summary>
    /// <param name="whitespace">Whitespace sequence.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(" ")]
    [Arguments("  ")]
    [Arguments("\t")]
    [Arguments("\n")]
    [Arguments("\r\n")]
    [Arguments(" \t \n ")]
    public async Task TextWhitespaceCollapses(string whitespace)
    {
        var input = $"<p>a{whitespace}b</p>";
        await Assert.That(Minify(input)).IsEqualTo("<p>a b</p>");
    }

    /// <summary>Various comment shapes are stripped under default options.</summary>
    /// <param name="comment">Comment payload.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<!-- comment -->")]
    [Arguments("<!---->")]
    [Arguments("<!-- -- inside -->")]
    [Arguments("<!--\nmulti\nline\n-->")]
    public async Task CommentsAreStripped(string comment) =>
        await Assert.That(Minify($"a{comment}b")).IsEqualTo("ab");

    /// <summary>Drives the minifier with default options.</summary>
    /// <param name="html">Source.</param>
    /// <returns>Minified output.</returns>
    private static string Minify(string html)
    {
        var sink = new ArrayBufferWriter<byte>(Math.Max(1, html.Length));
        HtmlMinifier.Minify(Encoding.UTF8.GetBytes(html), sink, HtmlMinifyOptions.Default);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
