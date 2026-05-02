// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>
/// Edge-case tests for the byte-level URL rewriters that back
/// <see cref="ExternalUrlScanner"/>. The existing scanner tests
/// exercise the happy paths via the public API; these focus on the
/// case-folding, word-boundary, and Unicode edges that motivated
/// the rewrite away from <see cref="System.Text.RegularExpressions.Regex"/>.
/// </summary>
public class UrlBytesTests
{
    /// <summary>Mixed-case <c>SRC=</c> / <c>HREF=</c> attribute names are recognized.</summary>
    /// <param name="html">Input.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<img SRC=\"https://cdn.test/a.png\">")]
    [Arguments("<a HREF=\"https://example.com\">x</a>")]
    [Arguments("<img Src=\"https://cdn.test/a.png\">")]
    public async Task AssetAttrCaseInsensitive(string html)
    {
        var (registry, filter) = MakeAllAccept();
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(html), registry, filter));
        await Assert.That(output).Contains("\"/local/", StringComparison.Ordinal);
    }

    /// <summary>Substrings without a word boundary do not match (<c>datasrc</c> ≠ <c>src</c>).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AssetAttrWordBoundaryRequired()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "<x datasrc=\"https://cdn.test/a.png\">";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).IsEqualTo(Html);
    }

    /// <summary>Multibyte UTF-8 around an asset URL is preserved verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnicodeSurroundsAssetAttr()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "前 <img src=\"https://cdn.test/a.png\"> 後 🚀";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).StartsWith("前 ");
        await Assert.That(output).EndsWith(" 後 🚀");
        await Assert.That(output).Contains("/local/");
    }

    /// <summary>Single-quoted asset values are supported.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AssetAttrSingleQuoted()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "<img src='https://cdn.test/a.png'>";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).Contains("'/local/");
    }

    /// <summary>Whitespace around the <c>=</c> in <c>src</c> attributes is tolerated.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AssetAttrWhitespaceAroundEq()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "<img src = \"https://cdn.test/a.png\">";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).Contains("/local/");
    }

    /// <summary>Multiple asset URLs on one page all rewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleAssetUrlsAllRewrite()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "<img src=\"https://cdn.test/a.png\"><img src=\"https://cdn.test/b.png\">";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        var first = output.IndexOf("/local/", StringComparison.Ordinal);
        var second = output.IndexOf("/local/", first + 1, StringComparison.Ordinal);
        await Assert.That(first).IsGreaterThanOrEqualTo(0);
        await Assert.That(second).IsGreaterThan(first);
    }

    /// <summary>Filter rejection leaves the URL untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FilterRejectionPassesThrough()
    {
        var registry = new ExternalAssetRegistry("assets/external"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: PrivacyTestHelpers.Utf8("other.test"));
        const string Html = "<img src=\"https://cdn.test/a.png\">";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).IsEqualTo(Html);
    }

    /// <summary>Mixed case + Unicode in srcset entries — rewrite the URL but preserve descriptors and Unicode neighbours.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SrcsetMixedCaseAndUnicode()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "前 <img SRCSET=\"https://cdn.test/a.png 1x, https://cdn.test/b.png 2x\"> 後";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).Contains("前 ");
        await Assert.That(output).Contains(" 後");
        await Assert.That(output).Contains("/local/");
        await Assert.That(output).Contains(" 1x");
        await Assert.That(output).Contains(" 2x");
    }

    /// <summary>Srcset value with no localizable URLs is left untouched (no rewrite happens).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SrcsetNoLocalizeUnchanged()
    {
        var registry = new ExternalAssetRegistry("assets/external"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: PrivacyTestHelpers.Utf8("other.test"));
        const string Html = "<img srcset=\"https://cdn.test/a.png 1x\">";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).IsEqualTo(Html);
    }

    /// <summary>CSS <c>url(...)</c> tokens inside a <c>&lt;style&gt;</c> block rewrite; tokens outside are left for audit-only.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineStyleBlockRewritesUrls()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "<p>before url(https://cdn.test/before.png)</p>"
            + "<style>.x { background: url(https://cdn.test/a.png); }</style>"
            + "<p>after url(https://cdn.test/after.png)</p>";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));

        // Inside <style>: rewritten.
        await Assert.That(output).Contains("url(/local/");

        // Outside <style>: untouched (Rewrite scope; Audit is broader).
        await Assert.That(output).Contains("url(https://cdn.test/before.png)");
        await Assert.That(output).Contains("url(https://cdn.test/after.png)");
    }

    /// <summary>Style tag attribute (<c>&lt;style type="..."&gt;</c>) is recognized; body URLs rewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StyleTagWithAttributesStillMatches()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "<style type=\"text/css\">.x { background: url(https://cdn.test/a.png); }</style>";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).Contains("url(/local/");
    }

    /// <summary>CSS url() supports double-quoted, single-quoted, and unquoted forms.</summary>
    /// <param name="bodyForm">Different quote forms.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("url(https://cdn.test/a.png)")]
    [Arguments("url(\"https://cdn.test/a.png\")")]
    [Arguments("url('https://cdn.test/a.png')")]
    [Arguments("url(  https://cdn.test/a.png  )")]
    public async Task CssUrlAllQuoteForms(string bodyForm)
    {
        var (registry, filter) = MakeAllAccept();
        var html = $"<style>.x{{background:{bodyForm};}}</style>";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(html), registry, filter));
        await Assert.That(output).Contains("/local/");
    }

    /// <summary>Substring <c>blurb(</c> doesn't match <c>url(</c> — the byte scanner advances correctly when the candidate fails.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NotUrlTokenAdvancesCleanly()
    {
        var (registry, filter) = MakeAllAccept();
        const string Html = "<style>.x { background: blurb(https://cdn.test/a.png); }</style>";
        var output = Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(Html), registry, filter));
        await Assert.That(output).IsEqualTo(Html);
    }

    /// <summary>Audit walks all three URL surfaces (asset attr, srcset, css url()).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AuditCollectsFromAllThreeSurfaces()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: PrivacyTestHelpers.Utf8("cdn.test"));
        var auditSet = new ConcurrentDictionary<byte[], byte>(ByteArrayComparer.Instance);
        const string Html = "<img src=\"https://cdn.test/a.png\">"
            + "<img srcset=\"https://cdn.test/b.png 2x\">"
            + "<style>.x { background: url(https://cdn.test/c.png); }</style>";
        ExternalUrlScanner.Audit(Encoding.UTF8.GetBytes(Html), filter, auditSet);
        await Assert.That(auditSet.ContainsKey("https://cdn.test/a.png"u8.ToArray())).IsTrue();
        await Assert.That(auditSet.ContainsKey("https://cdn.test/b.png"u8.ToArray())).IsTrue();
        await Assert.That(auditSet.ContainsKey("https://cdn.test/c.png"u8.ToArray())).IsTrue();
    }

    /// <summary>Filter rejection keeps the URL out of the audit set.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AuditRespectsFilter()
    {
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: PrivacyTestHelpers.Utf8("only.test"));
        var auditSet = new ConcurrentDictionary<byte[], byte>(ByteArrayComparer.Instance);
        const string Html = "<img src=\"https://cdn.test/a.png\">";
        ExternalUrlScanner.Audit(Encoding.UTF8.GetBytes(Html), filter, auditSet);
        await Assert.That(auditSet).IsEmpty();
    }

    /// <summary>Helper: registry that accepts everything plus a host filter that allow-lists the test CDN.</summary>
    /// <returns>Tuple of registry and filter.</returns>
    private static (ExternalAssetRegistry Registry, HostFilter Filter) MakeAllAccept()
    {
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        return (registry, filter);
    }
}
