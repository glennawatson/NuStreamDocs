// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;
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
        byte[] src = [.. "rest"u8];
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
        byte[] src = [.. "\"hello\" tail"u8];
        var p = 0;
        await Assert.That(OpenerLineParser.TryParseTitle(src, ref p, out var ts, out var tl)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(src, ts, tl)).IsEqualTo("hello");
    }

    /// <summary>Unterminated title returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnterminatedTitleReturnsFalse()
    {
        byte[] src = [.. "\"hello\n"u8];
        var p = 0;
        await Assert.That(OpenerLineParser.TryParseTitle(src, ref p, out _, out _)).IsFalse();
    }

    /// <summary>End-of-input mid-title returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EndOfInputMidTitleReturnsFalse()
    {
        byte[] src = [.. "\"hello"u8];
        var p = 0;
        await Assert.That(OpenerLineParser.TryParseTitle(src, ref p, out _, out _)).IsFalse();
    }

    /// <summary>ScanWhile advances past matching bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScanWhileBranches()
    {
        byte[] src = [.. "aaab"u8];
        var end = OpenerLineParser.ScanWhile(src, 0, b => b is (byte)'a');
        await Assert.That(end).IsEqualTo(3);
        var endAtEnd = OpenerLineParser.ScanWhile(src, 4, b => b is (byte)'a');
        await Assert.That(endAtEnd).IsEqualTo(4);
    }

    /// <summary>Slug-byte predicate accepts identifier bytes plus dash.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SlugByteBranches()
    {
        await Assert.That(AsciiByteHelpers.IsAsciiSlugByte((byte)'a')).IsTrue();
        await Assert.That(AsciiByteHelpers.IsAsciiSlugByte((byte)'Z')).IsTrue();
        await Assert.That(AsciiByteHelpers.IsAsciiSlugByte((byte)'9')).IsTrue();
        await Assert.That(AsciiByteHelpers.IsAsciiSlugByte((byte)'-')).IsTrue();
        await Assert.That(AsciiByteHelpers.IsAsciiSlugByte((byte)'_')).IsTrue();
        await Assert.That(AsciiByteHelpers.IsAsciiSlugByte((byte)' ')).IsFalse();
        await Assert.That(AsciiByteHelpers.IsAsciiSlugByte((byte)'!')).IsFalse();
    }

    /// <summary>Whitespace + trailing-header predicate cases.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WhitespacePredicates()
    {
        await Assert.That(AsciiByteHelpers.IsAsciiHorizontalWhitespace((byte)' ')).IsTrue();
        await Assert.That(AsciiByteHelpers.IsAsciiHorizontalWhitespace((byte)'\t')).IsTrue();
        await Assert.That(AsciiByteHelpers.IsAsciiHorizontalWhitespace((byte)'\n')).IsFalse();
        await Assert.That(OpenerLineParser.IsTrailingHeaderByte((byte)'\r')).IsTrue();
        await Assert.That(OpenerLineParser.IsTrailingHeaderByte((byte)'\n')).IsFalse();
    }
}
