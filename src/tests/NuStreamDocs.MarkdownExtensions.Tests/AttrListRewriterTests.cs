// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.MarkdownExtensions.AttrList;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behaviour tests for <c>AttrListRewriter</c>.</summary>
public class AttrListRewriterTests
{
    /// <summary>A heading with a trailing <c>{: #id .class }</c> token lifts both onto the opening tag.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LiftsBlockIdAndClass()
    {
        var output = Rewrite("<h1>Heading {: #intro .lead }</h1>");
        await Assert.That(output).IsEqualTo("<h1 id=\"intro\" class=\"lead\">Heading</h1>");
    }

    /// <summary>An anchor followed by a <c>{: target="_blank" }</c> token lifts the kv pair.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LiftsInlinePairedKeyValue()
    {
        var output = Rewrite("<p>See <a href=\"https://x.test\">here</a>{: target=\"_blank\" } for more.</p>");
        await Assert.That(output).Contains("<a href=\"https://x.test\" target=\"_blank\">here</a>");
        await Assert.That(output).DoesNotContain("{:");
    }

    /// <summary>An <c>img</c> followed by a <c>{: }</c> token lifts the attribute onto the void element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LiftsInlineVoidAttribute()
    {
        var output = Rewrite("<p><img src=\"/x.png\" alt=\"x\">{: .hero }</p>");
        await Assert.That(output).Contains("<img src=\"/x.png\" alt=\"x\" class=\"hero\">");
    }

    /// <summary>An existing class attribute is appended to (not overwritten by) attr-list classes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AppendsClassAttribute()
    {
        var output = Rewrite("<p class=\"existing\">Text {: .extra }</p>");
        await Assert.That(output).Contains("class=\"existing extra\"");
    }

    /// <summary>HTML without the <c>{:</c> marker is left untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PassesThroughWhenNoMarker()
    {
        const string Html = "<h1>Heading</h1><p>Body</p>";
        await Assert.That(AttrListRewriter.NeedsRewrite(Encoding.UTF8.GetBytes(Html))).IsFalse();
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">HTML input.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Rewrite(string source)
    {
        var sink = new System.Buffers.ArrayBufferWriter<byte>();
        AttrListRewriter.RewriteInto(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
