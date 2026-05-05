// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Direct tests for the shared Utf8StringWriter helpers.</summary>
public class Utf8StringWriterTests
{
    /// <summary>Various string inputs round-trip through Write.</summary>
    /// <param name="value">Source string.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("hello")]
    [Arguments("hello world")]
    [Arguments("café")]
    [Arguments("日本語")]
    [Arguments("emoji😀")]
    public async Task WriteRoundTrip(string value)
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8StringWriter.Write(sink, value);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(value);
    }

    /// <summary>Empty / null strings are no-ops.</summary>
    /// <param name="value">Source string.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("")]
    [Arguments(null!)]
    public async Task WriteEmptyOrNullNoOp(string? value)
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8StringWriter.Write(sink, value!);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Bulk-byte Write copies the input span verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteByteSpanCopies()
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8StringWriter.Write(sink, "hello-world"u8);
        await Assert.That(sink.WrittenSpan.SequenceEqual("hello-world"u8)).IsTrue();
    }

    /// <summary>Bulk-byte Write of an empty span advances no bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteByteSpanEmptyNoOp()
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8StringWriter.Write(sink, default(ReadOnlySpan<byte>));
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Sequential byte-span writes concatenate without intermediate transcoding.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteByteSpanSequentialConcatenates()
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8StringWriter.Write(sink, "<a "u8);
        Utf8StringWriter.Write(sink, "href=\"#x\""u8);
        Utf8StringWriter.Write(sink, ">"u8);
        await Assert.That(sink.WrittenSpan.SequenceEqual("<a href=\"#x\">"u8)).IsTrue();
    }

    /// <summary>Bulk-byte Write null-checks its sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteByteSpanNullSinkThrows() =>
        await Assert.That(() => Utf8StringWriter.Write(null!, "x"u8))
            .Throws<ArgumentNullException>();

    /// <summary>WriteByte advances by exactly one byte and writes the literal value.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteByteSingleByte()
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8StringWriter.WriteByte(sink, (byte)'X');
        await Assert.That(sink.WrittenCount).IsEqualTo(1);
        await Assert.That(sink.WrittenSpan[0]).IsEqualTo((byte)'X');
    }

    /// <summary>Repeated WriteByte calls concatenate in order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteByteSequentialConcatenates()
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8StringWriter.WriteByte(sink, (byte)' ');
        Utf8StringWriter.WriteByte(sink, (byte)'i');
        Utf8StringWriter.WriteByte(sink, (byte)'d');
        Utf8StringWriter.WriteByte(sink, (byte)'=');
        await Assert.That(sink.WrittenSpan.SequenceEqual(" id="u8)).IsTrue();
    }

    /// <summary>WriteByte null-checks its sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteByteNullSinkThrows() =>
        await Assert.That(() => Utf8StringWriter.WriteByte(null!, (byte)'a'))
            .Throws<ArgumentNullException>();

    /// <summary>WriteInt32 emits ASCII digits.</summary>
    /// <param name="value">Integer.</param>
    /// <param name="expected">Expected ASCII rendering.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(0, "0")]
    [Arguments(1, "1")]
    [Arguments(42, "42")]
    [Arguments(-7, "-7")]
    [Arguments(int.MaxValue, "2147483647")]
    public async Task WriteInt32RoundTrip(int value, string expected)
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8StringWriter.WriteInt32(sink, value);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(expected);
    }
}
