// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.CriticMarkup;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>CriticMarkupRewriter</c>.</summary>
public class CriticMarkupRewriterTests
{
    /// <summary><c>{++text++}</c> renders as <c>&lt;ins&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InsertSpanWraps() => await Assert.That(Rewrite("a {++inserted++} b")).IsEqualTo("a <ins>inserted</ins> b");

    /// <summary><c>{--text--}</c> renders as <c>&lt;del&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DeleteSpanWraps() => await Assert.That(Rewrite("a {--gone--} b")).IsEqualTo("a <del>gone</del> b");

    /// <summary><c>{==text==}</c> renders as <c>&lt;mark&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HighlightSpanWraps() => await Assert.That(Rewrite("a {==hi==} b")).IsEqualTo("a <mark>hi</mark> b");

    /// <summary><c>{&gt;&gt;text&lt;&lt;}</c> renders as a critic-comment span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CommentSpanWraps() =>
        await Assert.That(Rewrite("a {>>note<<} b"))
            .IsEqualTo("a <span class=\"critic comment\">note</span> b");

    /// <summary><c>{~~old~&gt;new~~}</c> renders as paired <c>&lt;del&gt;</c> + <c>&lt;ins&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubstituteSpanWraps() =>
        await Assert.That(Rewrite("a {~~old~>new~~} b"))
            .IsEqualTo("a <del>old</del><ins>new</ins> b");

    /// <summary><c>{~~text~~}</c> with no arrow renders as a plain <c>&lt;del&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubstituteWithoutArrowFallsBackToDelete() => await Assert.That(Rewrite("a {~~just~~} b")).IsEqualTo("a <del>just</del> b");

    /// <summary>Multiple span types coexist on a line.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleSpansCoexist() =>
        await Assert.That(Rewrite("{++a++} and {--b--} and {==c==}"))
            .IsEqualTo("<ins>a</ins> and <del>b</del> and <mark>c</mark>");

    /// <summary>Fenced code blocks pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "{++a++}\n```\n{++b++}\n```\nAfter {++c++}.";
        await Assert.That(Rewrite(Source))
            .IsEqualTo("<ins>a</ins>\n```\n{++b++}\n```\nAfter <ins>c</ins>.");
    }

    /// <summary>Inline code spans pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassesThrough() =>
        await Assert.That(Rewrite("use `{++literal++}` and {++wrapped++}"))
            .IsEqualTo("use `{++literal++}` and <ins>wrapped</ins>");

    /// <summary>Plain text round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "No CriticMarkup spans here.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Rewrites <paramref name="input"/> via <c>CriticMarkupRewriter</c>.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        CriticMarkupRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
