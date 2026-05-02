// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Links;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>MarkdownLinkRewriter</c>.</summary>
public class MarkdownLinkRewriterTests
{
    /// <summary>A relative <c>.md</c> href is rewritten to <c>.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativeMarkdownHrefIsRewritten() =>
        await Assert.That(Rewrite("<a href=\"other.md\">x</a>"))
            .IsEqualTo("<a href=\"other.html\">x</a>");

    /// <summary>Relative href with a sub-path is rewritten.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubPathHrefIsRewritten() =>
        await Assert.That(Rewrite("<a href=\"sub/page.md\">x</a>"))
            .IsEqualTo("<a href=\"sub/page.html\">x</a>");

    /// <summary>Anchors are preserved when rewriting.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AnchorIsPreserved() =>
        await Assert.That(Rewrite("<a href=\"page.md#section\">x</a>"))
            .IsEqualTo("<a href=\"page.html#section\">x</a>");

    /// <summary>Query strings are preserved when rewriting.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task QueryIsPreserved() =>
        await Assert.That(Rewrite("<a href=\"page.md?v=1\">x</a>"))
            .IsEqualTo("<a href=\"page.html?v=1\">x</a>");

    /// <summary>Absolute URLs are passed through unchanged even when ending in <c>.md</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AbsoluteHttpsUrlIsLeftAlone()
    {
        const string Source = "<a href=\"https://github.com/foo/README.md\">x</a>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Server-root and protocol-relative paths are passed through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RootRelativeIsLeftAlone()
    {
        await Assert.That(Rewrite("<a href=\"/page.md\">x</a>"))
            .IsEqualTo("<a href=\"/page.md\">x</a>");
        await Assert.That(Rewrite("<a href=\"//example.com/page.md\">x</a>"))
            .IsEqualTo("<a href=\"//example.com/page.md\">x</a>");
    }

    /// <summary><c>mailto:</c>, <c>tel:</c>, <c>ftp:</c> hrefs are passed through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonHttpSchemesAreLeftAlone()
    {
        await Assert.That(Rewrite("<a href=\"mailto:a@b.test\">x</a>"))
            .IsEqualTo("<a href=\"mailto:a@b.test\">x</a>");
        await Assert.That(Rewrite("<a href=\"tel:+15551234\">x</a>"))
            .IsEqualTo("<a href=\"tel:+15551234\">x</a>");
    }

    /// <summary>Hrefs that don't end in <c>.md</c> are passed through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonMarkdownHrefIsLeftAlone()
    {
        const string Source = "<a href=\"page.html\">x</a>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Anchor-only hrefs are passed through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AnchorOnlyHrefIsLeftAlone()
    {
        const string Source = "<a href=\"#section\">x</a>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Multiple links on one page each rewrite independently.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleLinksRewriteIndependently()
    {
        const string Source = "<a href=\"a.md\">a</a> mid <a href=\"https://x.test\">b</a> end <a href=\"c.md#x\">c</a>";
        const string Expected = "<a href=\"a.html\">a</a> mid <a href=\"https://x.test\">b</a> end <a href=\"c.html#x\">c</a>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary>HTML with no <c>href=</c> attributes short-circuits and round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlWithoutHrefsRoundTrips()
    {
        const string Source = "<p>plain paragraph</p>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Rewrites <paramref name="input"/> via <c>MarkdownLinkRewriter</c> and returns the result.</summary>
    /// <param name="input">HTML source.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Rewrite(string input) =>
        Encoding.UTF8.GetString(MarkdownLinkRewriter.Rewrite(Encoding.UTF8.GetBytes(input)));
}
