// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Behavior tests for <c>ExternalUrlScanner</c>'s <c>srcset</c> handling.</summary>
public class SrcsetRewriteTests
{
    /// <summary>Filter that allows every host.</summary>
    private static readonly HostFilter EmptyHosts = new(hostsToSkip: null, hostsAllowed: null);

    /// <summary>A single-URL srcset gets rewritten to the local path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesSingleUrlSrcset()
    {
        var registry = new ExternalAssetRegistry("assets/external"u8.ToArray());
        var output = Rewrite("<img srcset=\"https://example.com/x.png\">", registry);
        await Assert.That(output).Contains("srcset=\"/assets/external/");
        await Assert.That(output).DoesNotContain("https://example.com/x.png");
    }

    /// <summary>A multi-URL srcset rewrites every URL while preserving descriptors and commas.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesMultiUrlSrcsetPreservingDescriptors()
    {
        var registry = new ExternalAssetRegistry("assets/external"u8.ToArray());
        var output = Rewrite(
            "<img srcset=\"https://example.com/a.png 1x, https://example.com/b.png 2x\">",
            registry);
        await Assert.That(output).DoesNotContain("https://example.com/a.png");
        await Assert.That(output).DoesNotContain("https://example.com/b.png");
        await Assert.That(output).Contains(" 1x");
        await Assert.That(output).Contains(" 2x");
    }

    /// <summary>The descriptor on each entry survives the rewrite intact.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreservesWidthDescriptors()
    {
        var registry = new ExternalAssetRegistry("assets/external"u8.ToArray());
        var output = Rewrite(
            "<img srcset=\"https://example.com/sm.jpg 480w, https://example.com/lg.jpg 1080w\">",
            registry);
        await Assert.That(output).Contains(" 480w");
        await Assert.That(output).Contains(" 1080w");
    }

    /// <summary>A relative URL inside a srcset is not rewritten.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LeavesRelativeUrlsAloneInSrcset()
    {
        var registry = new ExternalAssetRegistry("assets/external"u8.ToArray());
        var output = Rewrite("<img srcset=\"/local/x.png 1x\">", registry);
        await Assert.That(output).IsEqualTo("<img srcset=\"/local/x.png 1x\">");
    }

    /// <summary>Helper that runs the scanner with the empty skip list.</summary>
    /// <param name="source">HTML input.</param>
    /// <param name="registry">URL registry.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Rewrite(string source, ExternalAssetRegistry registry) =>
        Encoding.UTF8.GetString(ExternalUrlScanner.Rewrite(Encoding.UTF8.GetBytes(source), registry, EmptyHosts));
}
