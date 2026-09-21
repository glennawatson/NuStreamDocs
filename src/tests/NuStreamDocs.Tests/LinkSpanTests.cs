// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Tests for the inline link parser.</summary>
public class LinkSpanTests
{
    /// <summary>Closing Link Offset used by the test cases.</summary>
    private const int ClosingLinkOffset = 7;

    /// <summary>Inputs whose shape is malformed return false from TryReadShape.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("text only")]
    [Arguments("[")]
    [Arguments("[unclosed")]
    [Arguments("[label]")]
    [Arguments("[label]not paren")]
    [Arguments("[label](no close")]
    public async Task TryReadShapeReturnsFalseOnMalformed(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var bytes = Encoding.UTF8.GetBytes(source);
        var bracket = source.IndexOf('[', StringComparison.Ordinal);
        if (bracket < 0)
        {
            await Assert.That(LinkSpan.TryReadShape(bytes, 0, out _)).IsFalse();
            return;
        }

        await Assert.That(LinkSpan.TryReadShape(bytes, bracket, out _)).IsFalse();
    }

    /// <summary>Well-formed shapes parse with the expected slice offsets.</summary>
    /// <param name="source">Source containing a single link at position 0.</param>
    /// <param name="expectedLabel">Expected label slice.</param>
    /// <param name="expectedHref">Expected href slice.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("[a](b)", "a", "b")]
    [Arguments("[label](https://x)", "label", "https://x")]
    [Arguments("[outer [inner] tail](u)", "outer [inner] tail", "u")]
    [Arguments("[l](http://h?q=(x))", "l", "http://h?q=(x)")]
    public async Task TryReadShapeWellFormed(string source, string expectedLabel, string expectedHref)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        await Assert.That(LinkSpan.TryReadShape(bytes, 0, out var shape)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(bytes.AsSpan(shape.LabelStart, shape.LabelEnd - shape.LabelStart)))
            .IsEqualTo(expectedLabel);
        await Assert.That(Encoding.UTF8.GetString(bytes.AsSpan(shape.HrefStart, shape.HrefEnd - shape.HrefStart)))
            .IsEqualTo(expectedHref);
    }

    /// <summary>FindMatching counts nested open/close pairs and returns -1 when unbalanced.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="start">Start offset (just past the implicit opener).</param>
    /// <param name="expected">Expected close index, or -1.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("abc)", 0, 3)]
    [Arguments("a(b)c)", 0, 5)]
    [Arguments("no close", 0, -1)]
    [Arguments("(()", 0, -1)]
    public async Task FindMatchingNesting(string source, int start, int expected) =>
        await Assert.That(LinkSpan.FindMatching(Encoding.UTF8.GetBytes(source), start, (byte)'(', (byte)')'))
            .IsEqualTo(expected);

    /// <summary>TryHandle on a complete shape writes an &lt;a&gt; element.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryHandleEmitsAnchor()
    {
        byte[] bytes = [.. "[hi](u)"u8];
        ArrayBufferWriter<byte> writer = new();
        var pos = 0;
        var pendingTextStart = 0;
        var handled = TryHandle(bytes, ref pos, ref pendingTextStart, writer);
        await Assert.That(handled).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("<a href=\"u\">hi</a>");
        await Assert.That(pos).IsEqualTo(ClosingLinkOffset);
    }

    /// <summary>TryHandle on a malformed shape advances nothing and returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryHandleMalformedReturnsFalse()
    {
        byte[] bytes = [.. "[unclosed"u8];
        ArrayBufferWriter<byte> writer = new();
        var pos = 0;
        var pendingTextStart = 0;
        var handled = TryHandle(bytes, ref pos, ref pendingTextStart, writer);
        await Assert.That(handled).IsFalse();
        await Assert.That(pos).IsEqualTo(0);
        await Assert.That(writer.WrittenCount).IsEqualTo(0);
    }

    /// <summary>While inline links are blocked, a link is left as literal text and the block stays in force.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryHandleLeavesLinkLiteralWhileBlocked()
    {
        byte[] bytes = [.. "[l](m)"u8];
        ArrayBufferWriter<byte> writer = new();
        var pos = 0;
        var pendingTextStart = 0;
        var result = TryHandleBlocked(bytes, ref pos, ref pendingTextStart, writer);
        await Assert.That(result.Handled).IsFalse();
        await Assert.That(result.StillBlocked).IsTrue();
        await Assert.That(pos).IsEqualTo(0);
        await Assert.That(writer.WrittenCount).IsEqualTo(0);
    }

    /// <summary>A link with whitespace before its destination renders while inline links are blocked, and ends the block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryHandleRendersMarkedLinkWhileBlocked()
    {
        byte[] bytes = [.. "[l]( /u)"u8];
        ArrayBufferWriter<byte> writer = new();
        var pos = 0;
        var pendingTextStart = 0;
        var result = TryHandleBlocked(bytes, ref pos, ref pendingTextStart, writer);
        await Assert.That(result.Handled).IsTrue();
        await Assert.That(result.StillBlocked).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("<a href=\"/u\">l</a>");
    }

    /// <summary>A link inside emphasis renders the inline links of its label as links.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryHandleNestsLabelLinksInsideEmphasis()
    {
        byte[] bytes = [.. "[a [l](m)](u)"u8];
        ArrayBufferWriter<byte> writer = new();
        var pos = 0;
        var pendingTextStart = 0;
        var handled = TryHandleInsideEmphasis(bytes, ref pos, ref pendingTextStart, writer);
        await Assert.That(handled).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("<a href=\"u\">a <a href=\"m\">l</a></a>");
    }

    /// <summary>Runs <see cref="LinkSpan.TryHandle"/> with inline links blocked.</summary>
    /// <param name="bytes">UTF-8 source.</param>
    /// <param name="pos">Cursor.</param>
    /// <param name="pendingTextStart">Start of the pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Whether a link was rendered and whether inline links are still blocked afterwards.</returns>
    private static BlockedLinkResult TryHandleBlocked(byte[] bytes, ref int pos, ref int pendingTextStart, ArrayBufferWriter<byte> writer)
    {
        EmphasisTable table = default;
        table.LinksBlocked = true;
        var handled = LinkSpan.TryHandle(bytes, ref pos, ref pendingTextStart, writer, ref table);
        return new(handled, table.LinksBlocked);
    }

    /// <summary>Runs <see cref="LinkSpan.TryHandle"/> with one emphasis span open around the link.</summary>
    /// <param name="bytes">UTF-8 source.</param>
    /// <param name="pos">Cursor.</param>
    /// <param name="pendingTextStart">Start of the pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when a link was rendered.</returns>
    private static bool TryHandleInsideEmphasis(byte[] bytes, ref int pos, ref int pendingTextStart, ArrayBufferWriter<byte> writer)
    {
        EmphasisTable table = default;
        table.Depth = 1;
        return LinkSpan.TryHandle(bytes, ref pos, ref pendingTextStart, writer, ref table);
    }

    /// <summary>Runs <see cref="LinkSpan.TryHandle"/> with a default render state.</summary>
    /// <param name="bytes">UTF-8 source.</param>
    /// <param name="pos">Cursor.</param>
    /// <param name="pendingTextStart">Start of the pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when a link was rendered.</returns>
    private static bool TryHandle(byte[] bytes, ref int pos, ref int pendingTextStart, ArrayBufferWriter<byte> writer)
    {
        EmphasisTable table = default;
        return LinkSpan.TryHandle(bytes, ref pos, ref pendingTextStart, writer, ref table);
    }

    /// <summary>Outcome of handling a link while inline links are blocked.</summary>
    /// <param name="Handled">True when a link was rendered.</param>
    /// <param name="StillBlocked">True when inline links are still blocked afterwards.</param>
    private sealed record BlockedLinkResult(bool Handled, bool StillBlocked);
}
