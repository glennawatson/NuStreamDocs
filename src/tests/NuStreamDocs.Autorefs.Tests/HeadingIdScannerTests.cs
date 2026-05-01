// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Behaviour tests for <c>HeadingIdScanner</c>.</summary>
public class HeadingIdScannerTests
{
    /// <summary>Each heading-with-id contributes one entry to the registry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsEveryHeadingId()
    {
        var registry = new AutorefsRegistry();
        var html = "<h1 id=\"intro\">Intro</h1>\n<p>body</p>\n<h2 id=\"detail\">Detail</h2>\n<h3>NoId</h3>"u8;
        HeadingIdScanner.ScanAndRegister(html, "guide/intro.html", registry);

        await Assert.That(registry.Count).IsEqualTo(2);
        await Assert.That(registry.TryResolve("intro", out var introUrl)).IsTrue();
        await Assert.That(introUrl).IsEqualTo("guide/intro.html#intro");
        await Assert.That(registry.TryResolve("detail", out var detailUrl)).IsTrue();
        await Assert.That(detailUrl).IsEqualTo("guide/intro.html#detail");
    }

    /// <summary>HTML without any heading IDs leaves the registry untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsHeadingsWithoutId()
    {
        var registry = new AutorefsRegistry();
        var html = "<p>just a paragraph</p>"u8;
        HeadingIdScanner.ScanAndRegister(html, "page.html", registry);
        await Assert.That(registry.Count).IsEqualTo(0);
    }
}
