// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Pins the core inline-emphasis renderer's behaviour against a fixed list of bold / italic / underscore edge cases.</summary>
public class BetterEmAuditTests
{
    /// <summary><c>**bold**</c> and <c>__bold__</c> both render as <c>&lt;strong&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BothBoldDelimitersRender()
    {
        await Assert.That(Render("**bold**")).Contains("<strong>bold</strong>");
        await Assert.That(Render("__bold__")).Contains("<strong>bold</strong>");
    }

    /// <summary><c>*italic*</c> and <c>_italic_</c> both render as <c>&lt;em&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BothItalicDelimitersRender()
    {
        await Assert.That(Render("*italic*")).Contains("<em>italic</em>");
        await Assert.That(Render("_italic_")).Contains("<em>italic</em>");
    }

    /// <summary>Mixed-marker nesting — bold containing italic — renders correctly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedNestingRenders()
    {
        var html = Render("**bold _and italic_ together**");
        await Assert.That(html).Contains("<strong>");
        await Assert.That(html).Contains("<em>and italic</em>");
    }

    /// <summary>Intra-word <c>_</c> should not trigger emphasis.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IntraWordUnderscoreDoesNotEmphasize()
    {
        var html = Render("foo_bar_baz handles snake_case");
        await Assert.That(html).DoesNotContain("<em>");
    }

    /// <summary>Triple <c>***</c> renders as combined bold+italic.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TripleStarYieldsBoldItalic()
    {
        var html = Render("***both***");
        await Assert.That(html).Contains("<strong>");
        await Assert.That(html).Contains("<em>");
    }

    /// <summary>Italic crossed-into-bold runs correctly nest.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedRunsBalance()
    {
        var html = Render("**a *b* c**");
        await Assert.That(html).Contains("<strong>");
        await Assert.That(html).Contains("<em>b</em>");
    }

    /// <summary>Renders <paramref name="markdown"/> through <c>MarkdownRenderer.Render</c> and returns the UTF-16 result.</summary>
    /// <param name="markdown">Source markdown.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(string markdown)
    {
        var bytes = Encoding.UTF8.GetBytes(markdown);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        MarkdownRenderer.Render(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
