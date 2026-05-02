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
        await Assert.That(Encoding.UTF8.GetString(bytes.AsSpan(shape.LabelStart, shape.LabelEnd - shape.LabelStart))).IsEqualTo(expectedLabel);
        await Assert.That(Encoding.UTF8.GetString(bytes.AsSpan(shape.HrefStart, shape.HrefEnd - shape.HrefStart))).IsEqualTo(expectedHref);
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
        var bytes = "[hi](u)"u8.ToArray();
        var writer = new ArrayBufferWriter<byte>();
        var pos = 0;
        var pendingTextStart = 0;
        await Assert.That(LinkSpan.TryHandle(bytes, ref pos, ref pendingTextStart, writer)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("<a href=\"u\">hi</a>");
        await Assert.That(pos).IsEqualTo(7);
    }

    /// <summary>TryHandle on a malformed shape advances nothing and returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryHandleMalformedReturnsFalse()
    {
        var bytes = "[unclosed"u8.ToArray();
        var writer = new ArrayBufferWriter<byte>();
        var pos = 0;
        var pendingTextStart = 0;
        await Assert.That(LinkSpan.TryHandle(bytes, ref pos, ref pendingTextStart, writer)).IsFalse();
        await Assert.That(pos).IsEqualTo(0);
        await Assert.That(writer.WrittenCount).IsEqualTo(0);
    }
}
