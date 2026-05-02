// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Behavior tests for <c>ExternalUrlScanner</c>.</summary>
public class ExternalUrlScannerTests
{
    /// <summary>Filter that allows every host (no skip, no allow-list).</summary>
    private static readonly HostFilter EmptyHosts = new(hostsToSkip: null, hostsAllowed: null);

    /// <summary>An <c>img src</c> with an absolute URL gets rewritten to the registry's local path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesAbsoluteImgSrc()
    {
        var registry = new ExternalAssetRegistry("assets/external");
        var output = Rewrite("<img src=\"https://example.com/x.png\" alt=\"x\">", registry, EmptyHosts);
        await Assert.That(output).Contains("src=\"/assets/external/");
        await Assert.That(output).DoesNotContain("https://example.com/x.png");
    }

    /// <summary>A <c>link href</c> stylesheet URL gets rewritten the same way.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesAbsoluteLinkHref()
    {
        var registry = new ExternalAssetRegistry("assets/external");
        var output = Rewrite("<link rel=\"stylesheet\" href=\"https://cdn.example/x.css\">", registry, EmptyHosts);
        await Assert.That(output).Contains("href=\"/assets/external/");
    }

    /// <summary>A relative <c>href</c> is left alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LeavesRelativeUrlsAlone()
    {
        var registry = new ExternalAssetRegistry("assets/external");
        var output = Rewrite("<a href=\"/local/page.html\">x</a>", registry, EmptyHosts);
        await Assert.That(output).IsEqualTo("<a href=\"/local/page.html\">x</a>");
    }

    /// <summary>A URL whose host is on the skip list is left external.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsHostsOnSkipList()
    {
        var registry = new ExternalAssetRegistry("assets/external");
        var skip = new HostFilter(hostsToSkip: ["trusted.cdn.example"], hostsAllowed: null);
        var output = Rewrite("<img src=\"https://trusted.cdn.example/x.png\">", registry, skip);
        await Assert.That(output).IsEqualTo("<img src=\"https://trusted.cdn.example/x.png\">");
    }

    /// <summary>The same URL maps to the same local path across multiple calls.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SameUrlMapsToSameLocalPath()
    {
        var registry = new ExternalAssetRegistry("assets/external");
        var first = registry.GetOrAdd("https://example.com/x.png");
        var second = registry.GetOrAdd("https://example.com/x.png");
        await Assert.That(first).IsEqualTo(second);
    }

    /// <summary>The pre-filter declines HTML that has no <c>http</c> substring.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreFilterRejectsHtmlWithNoUrls() => await Assert.That(ExternalUrlScanner.MayHaveExternalUrls("<p>no urls here</p>"u8)).IsFalse();

    /// <summary>RewriteString handles null input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteString_handles_null_input()
    {
        var registry = new ExternalAssetRegistry("assets");
        var filter = new HostFilter(null, null);
        var ex = Assert.Throws<ArgumentNullException>(() => ExternalUrlScanner.RewriteString(null!, registry, filter));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>RewriteString delegates to the byte walker.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteString_rewrites_html()
    {
        var registry = new ExternalAssetRegistry("/assets");
        var filter = new HostFilter(null, null);
        const string Input = """<img src="https://example.com/a.png">""";
        var output = ExternalUrlScanner.RewriteString(Input, registry, filter);
        await Assert.That(output).Contains("/assets/");
    }

    /// <summary>Helper that runs the scanner and returns the string result.</summary>
    /// <param name="source">HTML input.</param>
    /// <param name="registry">URL registry.</param>
    /// <param name="filter">Host filter.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Rewrite(string source, ExternalAssetRegistry registry, HostFilter filter) =>
        Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(source), registry, filter));
}
