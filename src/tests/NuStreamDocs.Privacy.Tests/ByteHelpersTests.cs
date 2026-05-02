// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Direct tests for the shared byte helpers used by every privacy byte scanner.</summary>
public class ByteHelpersTests
{
    /// <summary>ASCII identifier check covers letters, digits, underscore.</summary>
    /// <param name="b">Input byte.</param>
    /// <param name="expected">Expected.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments((byte)'A', true)]
    [Arguments((byte)'Z', true)]
    [Arguments((byte)'a', true)]
    [Arguments((byte)'z', true)]
    [Arguments((byte)'0', true)]
    [Arguments((byte)'9', true)]
    [Arguments((byte)'_', true)]
    [Arguments((byte)' ', false)]
    [Arguments((byte)'-', false)]
    [Arguments((byte)'.', false)]
    [Arguments((byte)'{', false)]
    [Arguments((byte)0x80, false)]
    public async Task IsAsciiIdentifierByteRecognisesIdentifierBytes(byte b, bool expected) => await Assert.That(AsciiByteHelpers.IsAsciiIdentifierByte(b)).IsEqualTo(expected);

    /// <summary>ASCII whitespace covers SP / HT / CR / LF only.</summary>
    /// <param name="b">Input byte.</param>
    /// <param name="expected">Expected.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments((byte)' ', true)]
    [Arguments((byte)'\t', true)]
    [Arguments((byte)'\r', true)]
    [Arguments((byte)'\n', true)]
    [Arguments((byte)'\v', false)]
    [Arguments((byte)'\f', false)]
    [Arguments((byte)0xA0, false)]
    [Arguments((byte)'a', false)]
    public async Task IsAsciiWhitespaceMatchesAsciiOnly(byte b, bool expected) => await Assert.That(AsciiByteHelpers.IsAsciiWhitespace(b)).IsEqualTo(expected);

    /// <summary>Word boundary at offset 0 is always true; otherwise depends on the prior byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IsWordBoundaryAtZeroIsAlwaysTrue() => await Assert.That(AsciiByteHelpers.IsWordBoundary("xyz"u8, 0)).IsTrue();

    /// <summary>Word boundary depends on whether the prior byte is an identifier byte.</summary>
    /// <param name="text">Buffer.</param>
    /// <param name="offset">Probe offset.</param>
    /// <param name="expected">Expected.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("a b", 2, true)]
    [Arguments("ab", 1, false)]
    [Arguments("a1", 1, false)]
    [Arguments("_b", 1, false)]
    [Arguments("-b", 1, true)]
    public async Task IsWordBoundaryRespectsPriorByte(string text, int offset, bool expected) =>
        await Assert.That(AsciiByteHelpers.IsWordBoundary(Encoding.ASCII.GetBytes(text), offset)).IsEqualTo(expected);

    /// <summary>Whitespace-skip stops at first non-whitespace.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SkipWhitespaceAdvancesOverRun()
    {
        await Assert.That(AsciiByteHelpers.SkipWhitespace("   x"u8, 0)).IsEqualTo(3);
        await Assert.That(AsciiByteHelpers.SkipWhitespace("\t \r\n y"u8, 0)).IsEqualTo(5);
        await Assert.That(AsciiByteHelpers.SkipWhitespace("abc"u8, 0)).IsEqualTo(0);
        await Assert.That(AsciiByteHelpers.SkipWhitespace("   "u8, 0)).IsEqualTo(3);
    }

    /// <summary>Case-insensitive prefix match: same length, mixed case, succeeds.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StartsWithIgnoreAsciiCaseMatchesMixedCase()
    {
        await Assert.That(AsciiByteHelpers.StartsWithIgnoreAsciiCase("HREF=\"x\""u8, 0, "href"u8)).IsTrue();
        await Assert.That(AsciiByteHelpers.StartsWithIgnoreAsciiCase("HrEf=\"x\""u8, 0, "href"u8)).IsTrue();
        await Assert.That(AsciiByteHelpers.StartsWithIgnoreAsciiCase("class=\"x\""u8, 0, "href"u8)).IsFalse();
    }

    /// <summary>Returns false when source is shorter than the prefix.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StartsWithIgnoreAsciiCaseRejectsShortSource()
    {
        await Assert.That(AsciiByteHelpers.StartsWithIgnoreAsciiCase("hr"u8, 0, "href"u8)).IsFalse();
        await Assert.That(AsciiByteHelpers.StartsWithIgnoreAsciiCase("xhref"u8, 2, "href"u8)).IsFalse();
    }

    /// <summary>The case-fold trick must not coerce non-letters into letters: '@' | 0x20 == '`' which is not 'a'.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CaseFoldDoesNotMatchNonLetterCollisions() => await Assert.That(AsciiByteHelpers.StartsWithIgnoreAsciiCase("@a"u8, 0, "ab"u8)).IsFalse();

    /// <summary>Equals treats unequal lengths as not equal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EqualsIgnoreAsciiCaseLengthMustMatch()
    {
        await Assert.That(AsciiByteHelpers.EqualsIgnoreAsciiCase("href"u8, "hrefx"u8)).IsFalse();
        await Assert.That(AsciiByteHelpers.EqualsIgnoreAsciiCase("hrefx"u8, "href"u8)).IsFalse();
    }

    /// <summary>Equals folds case across the whole span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EqualsIgnoreAsciiCaseFoldsCase()
    {
        await Assert.That(AsciiByteHelpers.EqualsIgnoreAsciiCase("LocalHost"u8, "localhost"u8)).IsTrue();
        await Assert.That(AsciiByteHelpers.EqualsIgnoreAsciiCase("LOCALHOST"u8, "localhost"u8)).IsTrue();
        await Assert.That(AsciiByteHelpers.EqualsIgnoreAsciiCase("example"u8, "localhost"u8)).IsFalse();
    }

    /// <summary>Encoding the empty string is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EncodeStringIntoEmptyIsNoOp()
    {
        var sink = new ArrayBufferWriter<byte>();
        AsciiByteHelpers.EncodeStringInto(string.Empty, sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Encoding a UTF-16 string with multibyte code points lands the right UTF-8 bytes in the sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EncodeStringIntoEmitsUtf8()
    {
        var sink = new ArrayBufferWriter<byte>();
        AsciiByteHelpers.EncodeStringInto("héllo🚀", sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("héllo🚀");
    }
}
