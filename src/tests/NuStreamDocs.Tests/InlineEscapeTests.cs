// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Tests for backslash inline-escape handling.</summary>
public class InlineEscapeTests
{
    /// <summary>Each ASCII-punctuation backslash escape emits the literal character (with HTML escaping).</summary>
    /// <param name="escaped">Two-byte source: backslash + escapee.</param>
    /// <param name="expected">Rendered output.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("\\!", "!")]
    [Arguments("\\#", "#")]
    [Arguments("\\(", "(")]
    [Arguments("\\.", ".")]
    [Arguments("\\:", ":")]
    [Arguments("\\@", "@")]
    [Arguments("\\[", "[")]
    [Arguments("\\`", "`")]
    [Arguments("\\{", "{")]
    [Arguments("\\~", "~")]
    [Arguments("\\&", "&amp;")]
    [Arguments("\\<", "&lt;")]
    [Arguments("\\>", "&gt;")]
    [Arguments("\\\"", "&quot;")]
    public async Task PunctEscapesEmitLiteral(string escaped, string expected)
    {
        var bytes = Encoding.UTF8.GetBytes(escaped);
        var writer = new ArrayBufferWriter<byte>();
        var pos = 0;
        var pendingTextStart = 0;
        await Assert.That(InlineEscape.TryHandle(bytes, ref pos, ref pendingTextStart, writer)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo(expected);
        await Assert.That(pos).IsEqualTo(2);
    }

    /// <summary>Non-punctuation followers leave the cursor unchanged and return false.</summary>
    /// <param name="source">Two-byte source: backslash + non-punct.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("\\a")]
    [Arguments("\\Z")]
    [Arguments("\\0")]
    [Arguments("\\9")]
    [Arguments("\\ ")]
    public async Task NonPunctEscapesReturnFalse(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var writer = new ArrayBufferWriter<byte>();
        var pos = 0;
        var pendingTextStart = 0;
        await Assert.That(InlineEscape.TryHandle(bytes, ref pos, ref pendingTextStart, writer)).IsFalse();
        await Assert.That(pos).IsEqualTo(0);
        await Assert.That(writer.WrittenCount).IsEqualTo(0);
    }

    /// <summary>A trailing backslash with nothing to escape returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingBackslashReturnsFalse()
    {
        var bytes = "\\"u8.ToArray();
        var writer = new ArrayBufferWriter<byte>();
        var pos = 0;
        var pendingTextStart = 0;
        await Assert.That(InlineEscape.TryHandle(bytes, ref pos, ref pendingTextStart, writer)).IsFalse();
    }

    /// <summary>Pending text before the escape is flushed when the escape succeeds.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PendingTextFlushedOnSuccess()
    {
        var bytes = "hi\\!"u8.ToArray();
        var writer = new ArrayBufferWriter<byte>();
        var pos = 2;
        var pendingTextStart = 0;
        await Assert.That(InlineEscape.TryHandle(bytes, ref pos, ref pendingTextStart, writer)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("hi!");
        await Assert.That(pendingTextStart).IsEqualTo(4);
    }
}
