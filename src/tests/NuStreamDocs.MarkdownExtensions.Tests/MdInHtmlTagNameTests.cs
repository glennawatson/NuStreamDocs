// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.MdInHtml;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Parameterized tag-name + attribute-value coverage for MdInHtmlRewriter.</summary>
public class MdInHtmlTagNameTests
{
    /// <summary>Common block tags carrying <c>markdown="1"</c> all rewrite.</summary>
    /// <param name="tag">HTML tag name.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("div")]
    [Arguments("section")]
    [Arguments("article")]
    [Arguments("aside")]
    [Arguments("header")]
    [Arguments("footer")]
    [Arguments("main")]
    [Arguments("nav")]
    public async Task BlockTagsRewrite(string tag) =>
        await Assert.That(Rewrite($"<{tag} markdown=\"1\">x</{tag}>")).Contains("\n\nx\n\n");

    /// <summary>Recognized attribute values trigger rewrite.</summary>
    /// <param name="value">Markdown attribute value.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("1")]
    [Arguments("block")]
    [Arguments("span")]
    public async Task RecognizedAttributeValues(string value) =>
        await Assert.That(Rewrite($"<div markdown=\"{value}\">x</div>")).Contains("\n\nx\n\n");

    /// <summary>Unrecognized attribute values are passed through.</summary>
    /// <param name="value">Unsupported value.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("0")]
    [Arguments("yes")]
    [Arguments("true")]
    [Arguments("on")]
    [Arguments("")]
    public async Task UnrecognizedValuesPassThrough(string value) =>
        await Assert.That(Rewrite($"<div markdown=\"{value}\">x</div>"))
            .IsEqualTo($"<div markdown=\"{value}\">x</div>");

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="input">Source.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        MdInHtmlRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
