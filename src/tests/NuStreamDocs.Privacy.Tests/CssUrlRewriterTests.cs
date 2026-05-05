// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Behavior tests for <c>CssUrlRewriter</c>.</summary>
public class CssUrlRewriterTests
{
    /// <summary>An absolute http(s) URL inside <c>url()</c> is registered and rewritten to the local path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesAbsoluteUrl()
    {
        ExternalAssetRegistry registry = new([.. "assets/external"u8]);
        var output = Rewrite("@font-face { src: url(https://fonts.example/x.woff2) }", "https://example.com/fonts.css", registry);
        await Assert.That(output).Contains("url(/assets/external/");
        await Assert.That(output).DoesNotContain("https://fonts.example/x.woff2");
    }

    /// <summary>A relative URL inside <c>url()</c> is resolved against the CSS file's own URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ResolvesRelativeUrlAgainstBase()
    {
        ExternalAssetRegistry registry = new([.. "assets/external"u8]);
        var output = Rewrite("body { background: url(./bg.png) }", "https://example.com/styles/main.css", registry);
        await Assert.That(output).Contains("url(/assets/external/");

        var entries = registry.EntriesSnapshot();
        await Assert.That(entries).HasSingleItem();
        await Assert.That(entries[0].Url.AsSpan().SequenceEqual("https://example.com/styles/bg.png"u8)).IsTrue();
    }

    /// <summary>A <c>data:</c> URL is left alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LeavesDataUrlsAlone()
    {
        ExternalAssetRegistry registry = new([.. "assets/external"u8]);
        var output = Rewrite("a { background: url(data:image/png;base64,AAA) }", "https://example.com/x.css", registry);
        await Assert.That(output).Contains("url(data:image/png");
    }

    /// <summary>Quoted <c>url("...")</c> tokens preserve the quote style on output.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreservesQuoteStyle()
    {
        ExternalAssetRegistry registry = new([.. "assets/external"u8]);
        var output = Rewrite("a { src: url(\"https://x.test/a.woff2\") }", "https://x.test/", registry);
        await Assert.That(output).Contains("url(\"/assets/external/");
    }

    /// <summary>Helper that runs the rewriter against the given base URL.</summary>
    /// <param name="css">CSS source.</param>
    /// <param name="baseUrl">Base URL the CSS file came from.</param>
    /// <param name="registry">URL registry.</param>
    /// <returns>Rewritten CSS.</returns>
    private static string Rewrite(string css, string baseUrl, ExternalAssetRegistry registry) =>
        Encoding.UTF8.GetString(CssUrlRewriter.Rewrite(Encoding.UTF8.GetBytes(css), new(baseUrl), registry, new(hostsToSkip: null, hostsAllowed: null)));
}
