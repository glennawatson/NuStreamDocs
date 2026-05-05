// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Direct unit tests for the <see cref="AsciiByteHelpers"/> shared byte-level helpers.</summary>
public class AsciiByteHelpersTests
{
    /// <summary>RunLength counts the leading run of <paramref name="marker"/> from <paramref name="pos"/>.</summary>
    /// <param name="source">UTF-8 input.</param>
    /// <param name="pos">Probe offset.</param>
    /// <param name="marker">Marker byte.</param>
    /// <param name="expected">Expected run length.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("```text", 0, (byte)'`', 3)]
    [Arguments("``text", 0, (byte)'`', 2)]
    [Arguments("text```", 4, (byte)'`', 3)]
    [Arguments("***", 0, (byte)'*', 3)]
    [Arguments("***", 1, (byte)'_', 0)]
    [Arguments("", 0, (byte)'`', 0)]
    public async Task RunLengthCountsRun(string source, int pos, byte marker, int expected)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        await Assert.That(AsciiByteHelpers.RunLength(bytes, pos, marker)).IsEqualTo(expected);
    }

    /// <summary>RunLength stops at the end of the buffer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunLengthStopsAtEnd()
    {
        byte[] bytes = [.. "***"u8];
        await Assert.That(AsciiByteHelpers.RunLength(bytes, 1, (byte)'*')).IsEqualTo(2);
        await Assert.That(AsciiByteHelpers.RunLength(bytes, 3, (byte)'*')).IsEqualTo(0);
    }

    /// <summary>RunLength returns 0 when the probe offset is at or past the end.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunLengthOutOfRange()
    {
        byte[] bytes = [.. "abc"u8];
        await Assert.That(AsciiByteHelpers.RunLength(bytes, 3, (byte)'a')).IsEqualTo(0);
        await Assert.That(AsciiByteHelpers.RunLength(bytes, 100, (byte)'a')).IsEqualTo(0);
    }

    /// <summary>ToLowerCaseInvariant converts ASCII to lowercase.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToLowerCaseInvariantAscii()
    {
        byte[] input = [.. "HELLO world!"u8];
        byte[] expected = [.. "hello world!"u8];
        var actual = AsciiByteHelpers.ToLowerCaseInvariant(input);
        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>ToLowerCaseInvariant copies non-ASCII bytes verbatim — only ASCII <c>A</c>-<c>Z</c> are folded.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToLowerCaseInvariantPreservesNonAscii()
    {
        byte[] input = [.. "ΠΣ-Δ"u8];
        var actual = AsciiByteHelpers.ToLowerCaseInvariant(input);
        await Assert.That(actual.AsSpan().SequenceEqual(input)).IsTrue();
    }

    /// <summary>ToLowerCaseInvariant on an empty span returns an empty array (no allocation cliff).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToLowerCaseInvariantEmpty()
    {
        var actual = AsciiByteHelpers.ToLowerCaseInvariant(default);
        await Assert.That(actual.Length).IsEqualTo(0);
    }

    /// <summary>ToLowerCaseInvariant folds mixed-case ASCII while leaving digits and punctuation untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToLowerCaseInvariantFoldsMixedAscii()
    {
        byte[] input = [.. "Hello-World_123"u8];
        byte[] expected = [.. "hello-world_123"u8];
        var actual = AsciiByteHelpers.ToLowerCaseInvariant(input);
        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }
}
