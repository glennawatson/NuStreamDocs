// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tags.Tests;

/// <summary>Direct tests for <c>Utf8StackBuffer</c> — the stack-or-pool UTF-8 adapter for byte-first APIs that expose a string-shaped overload.</summary>
public class Utf8StackBufferTests
{
    /// <summary>Encoding a short ASCII string into a generous stack span fits inline (no rental).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShortAsciiFitsInStack()
    {
        var copy = EncodeAndCopy("https://example.com/x.png", Utf8StackBuffer.StackSize);
        await Assert.That(copy.AsSpan().SequenceEqual("https://example.com/x.png"u8)).IsTrue();
    }

    /// <summary>Encoding multibyte UTF-8 round-trips through the byte view.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultiByteRoundTrips()
    {
        var copy = EncodeAndCopy("https://café.test/path", Utf8StackBuffer.StackSize);
        await Assert.That(Encoding.UTF8.GetString(copy)).IsEqualTo("https://café.test/path");
    }

    /// <summary>An input larger than the supplied stack span rents from the pool transparently.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OversizedInputFallsBackToPool()
    {
        var big = new string('a', 32);
        var copy = EncodeAndCopy(big, stackBufferSize: 8);
        await Assert.That(copy.Length).IsEqualTo(32);
        await Assert.That(Encoding.UTF8.GetString(copy)).IsEqualTo(big);
    }

    /// <summary>Empty / null inputs throw at the boundary.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RejectsNullOrEmpty()
    {
        await Assert.That(() => MakeBuffer(null!)).Throws<ArgumentException>();
        await Assert.That(() => MakeBuffer(string.Empty)).Throws<ArgumentException>();
    }

    /// <summary>Encodes <paramref name="value"/> through a buffer sized at <paramref name="stackBufferSize"/>, copying the bytes out before await.</summary>
    /// <param name="value">String to encode.</param>
    /// <param name="stackBufferSize">Stack-span size to provide to the buffer.</param>
    /// <returns>Heap copy of the encoded UTF-8 bytes.</returns>
    private static byte[] EncodeAndCopy(string value, int stackBufferSize)
    {
        // The ref-struct buffer can't cross an await; copy out before returning.
        Span<byte> stack = stackalloc byte[Utf8StackBuffer.StackSize];

        // Slicing the stack span lets the test exercise the small-stack pool-fallback path.
        using var buf = new Utf8StackBuffer(value, stack[..stackBufferSize]);
        return buf.Bytes.ToArray();
    }

    /// <summary>Helper that constructs (and immediately disposes) a buffer; isolates the stackalloc from the async machinery.</summary>
    /// <param name="value">String value.</param>
    private static void MakeBuffer(string value)
    {
        Span<byte> stack = stackalloc byte[Utf8StackBuffer.StackSize];
        using var buf = new Utf8StackBuffer(value, stack);
        _ = buf.Bytes;
    }
}
