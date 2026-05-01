// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>
/// Edge-case tests for the byte-level anchor hardener — the cases
/// that motivated the rewrite away from <c>Regex</c>: word boundaries,
/// case folding on tag/attr names, multibyte UTF-8 inside link text,
/// quoted-value parsing, and rel-token deduplication.
/// </summary>
public class AnchorBytesTests
{
    /// <summary>External anchor without rel/target gains both attributes when configured.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExternalAnchorGainsRelAndTarget()
    {
        var output = Rewrite("<a href=\"https://example.com\">x</a>", addRel: true, addTarget: true);
        await Assert.That(output).Contains("rel=\"noopener noreferrer\"");
        await Assert.That(output).Contains("target=\"_blank\"");
    }

    /// <summary>Internal anchor (relative href) is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InternalAnchorUntouched()
    {
        const string Html = "<a href=\"/about\">x</a>";
        await Assert.That(Rewrite(Html, addRel: true, addTarget: true)).IsEqualTo(Html);
    }

    /// <summary>Anchor without a href is left untouched (no external URL to harden against).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AnchorWithoutHrefUntouched()
    {
        const string Html = "<a name=\"toc\">x</a>";
        await Assert.That(Rewrite(Html, addRel: true, addTarget: true)).IsEqualTo(Html);
    }

    /// <summary>Tag-name word boundary — <c>&lt;article&gt;</c> is not <c>&lt;a&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ArticleTagIsNotAnchor()
    {
        const string Html = "<article href=\"https://example.com\">x</article>";
        await Assert.That(Rewrite(Html, addRel: true, addTarget: true)).IsEqualTo(Html);
    }

    /// <summary>Mixed-case tag name <c>&lt;A&gt;</c> is recognised case-insensitively (HTML is case-insensitive on tag names).</summary>
    /// <param name="html">Input.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<A href=\"https://example.com\">x</A>")]
    [Arguments("<a HREF=\"https://example.com\">x</a>")]
    [Arguments("<A HrEf=\"https://example.com\">x</A>")]
    public async Task CaseInsensitiveTagAndAttrNames(string html)
    {
        var output = Rewrite(html, addRel: true, addTarget: false);
        await Assert.That(output).Contains("rel=\"noopener noreferrer\"");
    }

    /// <summary>Existing <c>rel="author"</c> gets <c>noopener noreferrer</c> appended.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelTokensAppendedToExisting()
    {
        var output = Rewrite("<a href=\"https://example.com\" rel=\"author\">x</a>", addRel: true, addTarget: false);
        await Assert.That(output).Contains("rel=\"author noopener noreferrer\"");
    }

    /// <summary>Already-present <c>noopener</c> token is not duplicated (case-insensitive match).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelTokenDedupCaseInsensitive()
    {
        var output = Rewrite("<a href=\"https://example.com\" rel=\"NOOPENER author\">x</a>", addRel: true, addTarget: false);

        // 'noopener' is already present (case-insensitive), only 'noreferrer' is appended.
        await Assert.That(output).Contains("rel=\"NOOPENER author noreferrer\"");

        // Make sure noopener isn't duplicated.
        var firstAtIndex = output.IndexOf("noopener", StringComparison.OrdinalIgnoreCase);
        var lastAtIndex = output.LastIndexOf("noopener", StringComparison.OrdinalIgnoreCase);
        await Assert.That(firstAtIndex).IsEqualTo(lastAtIndex);
    }

    /// <summary>Existing <c>target</c> attribute is preserved (we don't overwrite the user's choice).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExistingTargetIsPreserved()
    {
        var output = Rewrite("<a href=\"https://example.com\" target=\"_self\">x</a>", addRel: false, addTarget: true);
        await Assert.That(output).Contains("target=\"_self\"");
        await Assert.That(output.Contains("target=\"_blank\"", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>Single-quoted attribute values parse correctly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleQuotedHrefSupported()
    {
        var output = Rewrite("<a href='https://example.com'>x</a>", addRel: true, addTarget: false);
        await Assert.That(output).Contains("rel=\"noopener noreferrer\"");
    }

    /// <summary>Whitespace around <c>=</c> is tolerated (matches the regex's <c>\s*=\s*</c>).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WhitespaceAroundEqualsTolerated()
    {
        var output = Rewrite("<a href = \"https://example.com\">x</a>", addRel: true, addTarget: false);
        await Assert.That(output).Contains("rel=\"noopener noreferrer\"");
    }

    /// <summary>Multibyte UTF-8 link text and surrounding markup pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnicodeLinkTextPreserved()
    {
        var output = Rewrite("これは <a href=\"https://example.com\">テスト 🚀</a> です", addRel: true, addTarget: true);
        await Assert.That(output).Contains("これは ");
        await Assert.That(output).Contains("テスト 🚀");
        await Assert.That(output).Contains(" です");
        await Assert.That(output).Contains("rel=\"noopener noreferrer\"");
    }

    /// <summary>Both options off → false return, no rewrite even on external anchor.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOptionsNoRewrite()
    {
        var sink = new ArrayBufferWriter<byte>();
        var bytes = Encoding.UTF8.GetBytes("<a href=\"https://example.com\">x</a>");
        var changed = AnchorBytes.RewriteInto(bytes, addRelNoOpener: false, addTargetBlank: false, sink);
        await Assert.That(changed).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Multiple anchors on one page all harden.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleAnchorsAllHarden()
    {
        var output = Rewrite(
            "<a href=\"https://a.test\">a</a><a href=\"https://b.test\">b</a>",
            addRel: true,
            addTarget: false);
        var occurrences = 0;
        var idx = 0;
        while ((idx = output.IndexOf("rel=\"noopener noreferrer\"", idx, StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            idx++;
        }

        await Assert.That(occurrences).IsEqualTo(2);
    }

    /// <summary>Mixed external + internal: only external gets hardened.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedExternalInternalOnlyExternalHardens()
    {
        var output = Rewrite(
            "<a href=\"/about\">i</a><a href=\"https://example.com\">e</a>",
            addRel: true,
            addTarget: false);
        await Assert.That(output).Contains("href=\"/about\">i</a>");
        await Assert.That(output).Contains("rel=\"noopener noreferrer\"");

        // The internal anchor should not have rel.
        var internalIdx = output.IndexOf("\"/about\"", StringComparison.Ordinal);
        var firstRel = output.IndexOf("rel=", StringComparison.Ordinal);
        await Assert.That(firstRel).IsGreaterThan(internalIdx);
    }

    /// <summary>Anchor without closing <c>&gt;</c> — scanner stops cleanly without throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnterminatedAnchorPassesThrough()
    {
        const string Html = "<a href=\"https://example.com\"";
        await Assert.That(Rewrite(Html, addRel: true, addTarget: true)).IsEqualTo(Html);
    }

    /// <summary>Truncated href value (no closing quote) means the attribute parser fails — anchor passes through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedHrefValuePassesThrough()
    {
        const string Html = "<a href=\"https://example.com>x</a>";

        // We can't validate href, so the scan won't recognise this as external.
        var output = Rewrite(Html, addRel: true, addTarget: true);
        await Assert.That(output.Contains("rel=\"noopener noreferrer\"", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>HTTP-scheme href is treated as external (we add hardening even before any UpgradeMixedContent).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HttpSchemeIsExternal()
    {
        var output = Rewrite("<a href=\"http://example.com\">x</a>", addRel: true, addTarget: false);
        await Assert.That(output).Contains("rel=\"noopener noreferrer\"");
    }

    /// <summary>Mailto / data / javascript schemes are NOT external (regex required <c>https?://</c>).</summary>
    /// <param name="html">Input with non-http scheme.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<a href=\"mailto:user@example.com\">x</a>")]
    [Arguments("<a href=\"javascript:void(0)\">x</a>")]
    [Arguments("<a href=\"#section\">x</a>")]
    [Arguments("<a href=\"./relative\">x</a>")]
    public async Task NonHttpSchemesUntouched(string html) => await Assert.That(Rewrite(html, addRel: true, addTarget: true)).IsEqualTo(html);

    /// <summary>Helper that runs the byte rewrite and decodes the result.</summary>
    /// <param name="html">Input HTML.</param>
    /// <param name="addRel">Whether to merge rel tokens.</param>
    /// <param name="addTarget">Whether to add target="_blank" when missing.</param>
    /// <returns>Rewritten string, or the input when no rewrite happened.</returns>
    private static string Rewrite(string html, bool addRel, bool addTarget)
    {
        var sink = new ArrayBufferWriter<byte>();
        var changed = AnchorBytes.RewriteInto(Encoding.UTF8.GetBytes(html), addRel, addTarget, sink);
        return changed ? Encoding.UTF8.GetString(sink.WrittenSpan) : html;
    }
}
