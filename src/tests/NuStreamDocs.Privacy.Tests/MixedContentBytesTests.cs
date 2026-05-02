// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>
/// Edge-case tests for the byte-level <c>http://</c> upgrader —
/// the cases that motivated the rewrite away from <c>Regex</c>:
/// case folding, word boundaries, loopback exemptions, multibyte
/// UTF-8 in the host, and pass-through when no rewrite happens.
/// </summary>
public class MixedContentBytesTests
{
    /// <summary>Lowercase ASCII <c>href</c> upgrades.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LowercaseHrefUpgrades()
    {
        var output = Rewrite("<a href=\"http://example.com/p\">x</a>");
        await Assert.That(output).IsEqualTo("<a href=\"https://example.com/p\">x</a>");
    }

    /// <summary>Mixed-case attribute names are recognized case-insensitively (HTML is case-insensitive on tag/attr names).</summary>
    /// <param name="html">Input.</param>
    /// <param name="expected">Expected output.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<a HREF=\"http://example.com\">x", "<a HREF=\"https://example.com\">x")]
    [Arguments("<a HrEf=\"http://example.com\">x", "<a HrEf=\"https://example.com\">x")]
    [Arguments("<img SRC=\"http://example.com/a.png\">", "<img SRC=\"https://example.com/a.png\">")]
    [Arguments("<img Src=\"http://example.com/a.png\">", "<img Src=\"https://example.com/a.png\">")]
    public async Task AttributeNameCaseInsensitive(string html, string expected) => await Assert.That(Rewrite(html)).IsEqualTo(expected);

    /// <summary>The <c>http://</c> scheme itself is matched only in lowercase — uppercase is left untouched (browsers normalize it; we don't).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UppercaseSchemeIsNotTouched()
    {
        const string Html = "<a href=\"HTTP://example.com\">x</a>";
        await Assert.That(Rewrite(Html)).IsEqualTo(Html);
    }

    /// <summary>Loopback hosts must NOT be upgraded (browsers exempt them from mixed-content rules).</summary>
    /// <param name="html">Input that should be passed through unchanged.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<a href=\"http://localhost/\">x</a>")]
    [Arguments("<a href=\"http://LOCALHOST/\">x</a>")]
    [Arguments("<a href=\"http://127.0.0.1/\">x</a>")]
    [Arguments("<a href=\"http://127.255.255.254/\">x</a>")]
    [Arguments("<a href=\"http://[::1]/\">x</a>")]
    public async Task LoopbackHostsArePassedThrough(string html) => await Assert.That(Rewrite(html)).IsEqualTo(html);

    /// <summary>Hosts that merely *start with* <c>localhost</c> are NOT loopback (e.g. <c>localhost.example.com</c>) and DO upgrade.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LocalhostPrefixIsNotLoopback()
    {
        var output = Rewrite("<a href=\"http://localhost.example.com/\">x</a>");
        await Assert.That(output).IsEqualTo("<a href=\"https://localhost.example.com/\">x</a>");
    }

    /// <summary>Substrings that look like attribute names but aren't (no word boundary) are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoWordBoundaryNoMatch()
    {
        // "datasrc" is not the "src" attribute — leading 'a' is an
        // identifier byte, so the word-boundary check fails.
        const string Html = "<x datasrc=\"http://example.com\">";
        await Assert.That(Rewrite(Html)).IsEqualTo(Html);
    }

    /// <summary>Single-quoted attribute values are upgraded too.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleQuotedValueUpgrades()
    {
        var output = Rewrite("<a href='http://example.com'>x");
        await Assert.That(output).IsEqualTo("<a href='https://example.com'>x");
    }

    /// <summary>Unquoted attribute values are NOT recognized (the regex required quotes; the byte scanner matches that behavior).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnquotedValueIsLeftAlone()
    {
        const string Html = "<a href=http://example.com>x";
        await Assert.That(Rewrite(Html)).IsEqualTo(Html);
    }

    /// <summary>Multibyte UTF-8 IDN-style hosts pass through the host scanner without splitting a code point.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnicodeHostUpgrades()
    {
        var output = Rewrite("<a href=\"http://例え.example/p\">x</a>");
        await Assert.That(output).IsEqualTo("<a href=\"https://例え.example/p\">x</a>");
    }

    /// <summary>Surrounding multibyte UTF-8 content (emoji, CJK) is copied through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnicodeSurroundsAreCopiedVerbatim()
    {
        var output = Rewrite("これは <a href=\"http://example.com\">テスト</a> 🚀");
        await Assert.That(output).IsEqualTo("これは <a href=\"https://example.com\">テスト</a> 🚀");
    }

    /// <summary>Multiple matches in one document all upgrade.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleMatchesAllUpgrade()
    {
        var output = Rewrite(
            "<a href=\"http://a.test\">a</a> <a href=\"http://b.test\">b</a> <img src=\"http://c.test/x.png\">");
        await Assert.That(output).IsEqualTo(
            "<a href=\"https://a.test\">a</a> <a href=\"https://b.test\">b</a> <img src=\"https://c.test/x.png\">");
    }

    /// <summary>Mixed loopback + non-loopback in the same page upgrades only the non-loopback one.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedLoopbackOnlyNonLoopbackUpgrades()
    {
        var output = Rewrite("<a href=\"http://localhost\">a</a><a href=\"http://example.com\">b</a>");
        await Assert.That(output).IsEqualTo(
            "<a href=\"http://localhost\">a</a><a href=\"https://example.com\">b</a>");
    }

    /// <summary>Empty input → no-op (returns false, sink untouched).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputReturnsFalse()
    {
        var sink = new ArrayBufferWriter<byte>();
        var changed = MixedContentBytes.RewriteInto([], sink);
        await Assert.That(changed).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Input with no candidates returns false and writes nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoMatchReturnsFalseAndWritesNothing()
    {
        var sink = new ArrayBufferWriter<byte>();
        var changed = MixedContentBytes.RewriteInto("hello world"u8, sink);
        await Assert.That(changed).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Truncated input — no complete <c>http://</c>+host present — passes through unchanged.</summary>
    /// <param name="html">Truncated input.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<a href=\"http")]
    [Arguments("<a href=\"http:")]
    [Arguments("<a href=\"https")]
    [Arguments("<a href=")]
    [Arguments("<a href=\"")]
    [Arguments("<a hr")]
    public async Task TruncatedInputBeforeSchemePassesThrough(string html) => await Assert.That(Rewrite(html)).IsEqualTo(html);

    /// <summary>Truncated input that already contains <c>http://</c> with an empty host doesn't throw — the scanner upgrades it to <c>https://</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TruncatedInputAfterSchemeStillUpgrades() =>

        // Empty host is not loopback, so it gets upgraded — the important
        // thing is no out-of-range read on the truncated buffer.
        await Assert.That(Rewrite("<a href=\"http://")).IsEqualTo("<a href=\"https://");

    /// <summary>Helper that runs the byte rewrite and decodes the result.</summary>
    /// <param name="html">Input HTML.</param>
    /// <returns>Rewritten string, or the input when no rewrite happened.</returns>
    private static string Rewrite(string html)
    {
        var sink = new ArrayBufferWriter<byte>();
        var changed = MixedContentBytes.RewriteInto(Encoding.UTF8.GetBytes(html), sink);
        return changed ? Encoding.UTF8.GetString(sink.WrittenSpan) : html;
    }
}
