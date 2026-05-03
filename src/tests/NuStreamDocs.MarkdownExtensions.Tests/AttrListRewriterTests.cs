// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.AttrList;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>AttrListRewriter</c>.</summary>
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

    /// <summary>The mkdocs-material shorthand <c>{ .class }</c> (open-brace + space, no colon) is recognized.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SpaceFormLiftsClass()
    {
        var output = Rewrite("<p><a href=\"/get-started/\">Get started</a>{ .md-button .md-button--primary }</p>");
        await Assert.That(output).Contains("<a href=\"/get-started/\" class=\"md-button md-button--primary\">Get started</a>");
        await Assert.That(output).DoesNotContain("{ .md-button");
    }

    /// <summary>The space form lifts a key/value pair onto a paired inline element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SpaceFormLiftsInlinePairedKeyValue()
    {
        var output = Rewrite("<p><a href=\"https://x.test\">here</a>{ target=\"_blank\" }</p>");
        await Assert.That(output).Contains("<a href=\"https://x.test\" target=\"_blank\">here</a>");
        await Assert.That(output).DoesNotContain("{ target=");
    }

    /// <summary>The space form lifts <c>#id</c> tokens onto block elements.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SpaceFormLiftsBlockId()
    {
        var output = Rewrite("<h1>Heading { #intro }</h1>");
        await Assert.That(output).Contains("id=\"intro\"");
    }

    /// <summary>The space form applies classes to inline SVG so icon shortcodes can size correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SpaceFormLiftsSvgClasses()
    {
        var output = Rewrite("<p><svg viewBox=\"0 0 24 24\"></svg>{ .lg .middle }</p>");
        await Assert.That(output).Contains("<svg viewBox=\"0 0 24 24\" class=\"lg middle\"></svg>");
        await Assert.That(output).DoesNotContain("{ .lg .middle }");
    }

    /// <summary>An incidental <c>{ </c> in a code block (no attr-list lead bytes after the brace) is left alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IncidentalBraceInCodeBlockIsIgnored()
    {
        // The gate is permissive (any `{ ` triggers it) but the per-position matcher rejects
        // because `{ foo:` parses as kv-pair rather than an attr-list lead, and the body
        // contains a closing tag's `<` before the marker would close.
        const string Html = "<pre><code>var x = { foo: 1 };</code></pre>";
        var output = Rewrite(Html);
        await Assert.That(output).IsEqualTo(Html);
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">HTML input.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Rewrite(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        AttrListRewriter.RewriteInto(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
