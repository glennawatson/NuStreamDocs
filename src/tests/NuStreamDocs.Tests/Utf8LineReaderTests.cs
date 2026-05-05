// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Branch-coverage tests for the async UTF-8 line reader.</summary>
public class Utf8LineReaderTests
{
    /// <summary>LF and CR-LF terminators are both stripped from the returned slice.</summary>
    /// <param name="source">Source text.</param>
    /// <param name="expected">Expected concatenation of returned lines, separated by '|'.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("a\nb\nc\n", "a|b|c")]
    [Arguments("a\r\nb\r\nc\r\n", "a|b|c")]
    [Arguments("a\nb\r\nc", "a|b|c")]
    [Arguments("", "")]
    [Arguments("only-line", "only-line")]
    [Arguments("\n", "")]
    public async Task LineSeparatorShapes(string source, string expected)
    {
        var actual = string.Join('|', await ReadAll(source));
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>A line longer than the initial buffer triggers buffer growth.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LongLineGrowsBuffer()
    {
        string longLine = new('x', 32 * 1024);
        var lines = await ReadAll(longLine + "\nshort\n");
        await Assert.That(lines.Length).IsEqualTo(2);
        await Assert.That(lines[0].Length).IsEqualTo(32 * 1024);
        await Assert.That(lines[1]).IsEqualTo("short");
    }

    /// <summary>Disposing without leaveOpen disposes the underlying stream.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DisposeOwnedStreamDisposesIt()
    {
        MemoryStream stream = new([.. "a\n"u8]);
        using (new Utf8LineReader(stream, leaveOpen: false))
        {
            // ctor only
        }

        await Assert.That(stream.ReadByte).Throws<ObjectDisposedException>();
    }

    /// <summary>leaveOpen keeps the underlying stream usable after the reader disposes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LeaveOpenKeepsStreamAlive()
    {
        MemoryStream stream = new([.. "a\n"u8]);
        using (new Utf8LineReader(stream, leaveOpen: true))
        {
            // ctor only
        }

        await Assert.That(stream.CanRead).IsTrue();
    }

    /// <summary>Reads every line from <paramref name="source"/> via Utf8LineReader.</summary>
    /// <param name="source">UTF-8 source text.</param>
    /// <returns>Decoded lines.</returns>
    private static async Task<string[]> ReadAll(string source)
    {
        await using MemoryStream stream = new(Encoding.UTF8.GetBytes(source));
        using Utf8LineReader reader = new(stream, leaveOpen: true);
        List<string> lines = [];
        while (true)
        {
            var (hasLine, line) = await reader.TryReadLineAsync(CancellationToken.None);
            if (!hasLine)
            {
                break;
            }

            lines.Add(Encoding.UTF8.GetString(line.Span));
        }

        return [.. lines];
    }
}
