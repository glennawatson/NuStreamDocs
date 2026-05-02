// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.MarkdownExtensions.Internal;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Direct tests for the OpenerLineParser helpers.</summary>
public class OpenerLineParserTests
{
    /// <summary>Cursor not on a quote returns true and leaves cursor unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoTitleReturnsTrue()
    {
        var src = "rest"u8.ToArray();
        var p = 0;
        await Assert.That(OpenerLineParser.TryParseTitle(src, ref p, out var ts, out var tl)).IsTrue();
        await Assert.That(p).IsEqualTo(0);
        await Assert.That(tl).IsEqualTo(0);
        _ = ts;
    }

    /// <summary>A complete "title" advances the cursor past the closing quote.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ParsesCompleteTitle()
    {
        var src = "\"hello\" tail"u8.ToArray();
        var p = 0;
        await Assert.That(OpenerLineParser.TryParseTitle(src, ref p, out var ts, out var tl)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(src, ts, tl)).IsEqualTo("hello");
    }

    /// <summary>Unterminated title returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnterminatedTitleReturnsFalse()
    {
        var src = "\"hello\n"u8.ToArray();
        var p = 0;
        await Assert.That(OpenerLineParser.TryParseTitle(src, ref p, out _, out _)).IsFalse();
    }

    /// <summary>End-of-input mid-title returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EndOfInputMidTitleReturnsFalse()
    {
        var src = "\"hello"u8.ToArray();
        var p = 0;
        await Assert.That(OpenerLineParser.TryParseTitle(src, ref p, out _, out _)).IsFalse();
    }

    /// <summary>ScanWhile advances past matching bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScanWhileBranches()
    {
        var src = "aaab"u8.ToArray();
        var end = OpenerLineParser.ScanWhile(src, 0, b => b is (byte)'a');
        await Assert.That(end).IsEqualTo(3);
        var endAtEnd = OpenerLineParser.ScanWhile(src, 4, b => b is (byte)'a');
        await Assert.That(endAtEnd).IsEqualTo(4);
    }

    /// <summary>Type-char predicate accepts identifier bytes only.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TypeCharBranches()
    {
        await Assert.That(OpenerLineParser.IsTypeChar((byte)'a')).IsTrue();
        await Assert.That(OpenerLineParser.IsTypeChar((byte)'Z')).IsTrue();
        await Assert.That(OpenerLineParser.IsTypeChar((byte)'9')).IsTrue();
        await Assert.That(OpenerLineParser.IsTypeChar((byte)'-')).IsTrue();
        await Assert.That(OpenerLineParser.IsTypeChar((byte)'_')).IsTrue();
        await Assert.That(OpenerLineParser.IsTypeChar((byte)' ')).IsFalse();
        await Assert.That(OpenerLineParser.IsTypeChar((byte)'!')).IsFalse();
    }

    /// <summary>Whitespace + trailing-header predicate cases.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WhitespacePredicates()
    {
        await Assert.That(OpenerLineParser.IsHorizontalSpace((byte)' ')).IsTrue();
        await Assert.That(OpenerLineParser.IsHorizontalSpace((byte)'\t')).IsTrue();
        await Assert.That(OpenerLineParser.IsHorizontalSpace((byte)'\n')).IsFalse();
        await Assert.That(OpenerLineParser.IsTrailingHeaderByte((byte)'\r')).IsTrue();
        await Assert.That(OpenerLineParser.IsTrailingHeaderByte((byte)'\n')).IsFalse();
    }
}
