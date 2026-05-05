// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Unit tests for the shared CodeAwareRewriter scan loop.</summary>
public class CodeAwareRewriterTests
{
    /// <summary>Null writer is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullWriterRejected()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            CodeAwareRewriter.Run("hi"u8, writer: null!, NoopProbe));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Null probe is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullProbeRejected()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            CodeAwareRewriter.Run("hi"u8, new ArrayBufferWriter<byte>(), tryRewrite: null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Empty source produces empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput()
    {
        ArrayBufferWriter<byte> sink = new();
        CodeAwareRewriter.Run([], sink, NoopProbe);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Bytes pass through verbatim when the probe never matches.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PassthroughWhenProbeNeverMatches()
    {
        byte[] input = [.. "hello world"u8];
        ArrayBufferWriter<byte> sink = new();
        CodeAwareRewriter.Run(input, sink, NoopProbe);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("hello world");
    }

    /// <summary>Probe substitutions replace the matched range.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ProbeSubstitutes()
    {
        ArrayBufferWriter<byte> sink = new();
        CodeAwareRewriter.Run("[X]middle[X]"u8, sink, AngleBracketX);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("<x/>middle<x/>");
    }

    /// <summary>Inline code spans pass through and the probe never sees their interior.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassThrough()
    {
        ArrayBufferWriter<byte> sink = new();
        CodeAwareRewriter.Run("a `code [X] inside` b"u8, sink, AngleBracketX);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).IsEqualTo("a `code [X] inside` b");
    }

    /// <summary>Fenced-code regions pass through verbatim and the probe never fires inside.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassThrough()
    {
        ArrayBufferWriter<byte> sink = new();
        CodeAwareRewriter.Run("before\n```\n[X]\n```\nafter [X]"u8, sink, AngleBracketX);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("```\n[X]\n```");
        await Assert.That(output).Contains("after <x/>");
    }

    /// <summary>Probe that always returns false leaves source unchanged.</summary>
    /// <param name="source">Source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Always 0.</param>
    /// <returns>Always false.</returns>
    private static bool NoopProbe(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        return false;
    }

    /// <summary>Probe that rewrites <c>[X]</c> to <c>&lt;x/&gt;</c>.</summary>
    /// <param name="source">Source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on match.</param>
    /// <returns>True when the cursor sits on <c>[X]</c>.</returns>
    private static bool AngleBracketX(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        if (offset + 2 < source.Length
            && source[offset] is (byte)'['
            && source[offset + 1] is (byte)'X'
            && source[offset + 2] is (byte)']')
        {
            writer.Write("<x/>"u8);
            consumed = 3;
            return true;
        }

        consumed = 0;
        return false;
    }
}
