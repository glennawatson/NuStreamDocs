// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.CaretTilde;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behaviour tests for <c>CaretTildeRewriter</c>.</summary>
public class CaretTildeRewriterTests
{
    /// <summary>Single carets become <c>&lt;sup&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleCaretRewritesToSup() => await Assert.That(Rewrite("E = mc^2^ here")).IsEqualTo("E = mc<sup>2</sup> here");

    /// <summary>Doubled carets become <c>&lt;ins&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DoubleCaretRewritesToIns() => await Assert.That(Rewrite("a ^^new^^ thing")).IsEqualTo("a <ins>new</ins> thing");

    /// <summary>Single tildes become <c>&lt;sub&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleTildeRewritesToSub() => await Assert.That(Rewrite("H~2~O is water")).IsEqualTo("H<sub>2</sub>O is water");

    /// <summary>Doubled tildes become <c>&lt;del&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DoubleTildeRewritesToDel() => await Assert.That(Rewrite("~~strikeout~~ this")).IsEqualTo("<del>strikeout</del> this");

    /// <summary>Mixed markers in a single line all rewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedMarkersRewriteIndependently() =>
        await Assert.That(Rewrite("x^2^ + y~i~ ~~old~~ ^^new^^"))
            .IsEqualTo("x<sup>2</sup> + y<sub>i</sub> <del>old</del> <ins>new</ins>");

    /// <summary>Markers with whitespace immediately after the open are not closed and pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WhitespaceAfterOpenIsNotASpan() => await Assert.That(Rewrite("a ^ b ^ c")).IsEqualTo("a ^ b ^ c");

    /// <summary>Fenced code blocks pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "Outside ^a^.\n```\n^a^ ~b~\n```\nAfter ^c^.";
        const string Expected = "Outside <sup>a</sup>.\n```\n^a^ ~b~\n```\nAfter <sup>c</sup>.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary>Inline code spans pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassesThrough() =>
        await Assert.That(Rewrite("use `^a^` and ^b^"))
            .IsEqualTo("use `^a^` and <sup>b</sup>");

    /// <summary>Plain text round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "Just a paragraph with no markers.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input produces empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Unclosed marker at end of line passes through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedMarkerPassesThrough() => await Assert.That(Rewrite("trailing ^foo")).IsEqualTo("trailing ^foo");

    /// <summary>Rewrites <paramref name="input"/> via <c>CaretTildeRewriter</c>.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        CaretTildeRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
