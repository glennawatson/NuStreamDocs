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
        var bytes = "***"u8.ToArray();
        await Assert.That(AsciiByteHelpers.RunLength(bytes, 1, (byte)'*')).IsEqualTo(2);
        await Assert.That(AsciiByteHelpers.RunLength(bytes, 3, (byte)'*')).IsEqualTo(0);
    }

    /// <summary>RunLength returns 0 when the probe offset is at or past the end.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunLengthOutOfRange()
    {
        var bytes = "abc"u8.ToArray();
        await Assert.That(AsciiByteHelpers.RunLength(bytes, 3, (byte)'a')).IsEqualTo(0);
        await Assert.That(AsciiByteHelpers.RunLength(bytes, 100, (byte)'a')).IsEqualTo(0);
    }
}
