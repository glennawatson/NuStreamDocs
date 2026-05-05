// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Behavior tests for <c>HeadingIdScanner</c>.</summary>
public class HeadingIdScannerTests
{
    /// <summary>Each heading-with-id contributes one entry to the registry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsEveryHeadingId()
    {
        var registry = new AutorefsRegistry();
        var html = "<h1 id=\"intro\">Intro</h1>\n<p>body</p>\n<h2 id=\"detail\">Detail</h2>\n<h3>NoId</h3>"u8;
        HeadingIdScanner.ScanAndRegister(html, "guide/intro.html"u8.ToArray(), registry);

        await Assert.That(registry.Count).IsEqualTo(2);
        await Assert.That(registry.TryResolve("intro"u8, out var introUrl)).IsTrue();
        await Assert.That(introUrl.AsSpan().SequenceEqual("guide/intro.html#intro"u8)).IsTrue();
        await Assert.That(registry.TryResolve("detail"u8, out var detailUrl)).IsTrue();
        await Assert.That(detailUrl.AsSpan().SequenceEqual("guide/intro.html#detail"u8)).IsTrue();
    }

    /// <summary>Empty <c>&lt;a id="..."&gt;</c> anchors — the form the <c>[](){#id}</c> markdown shorthand expands to — also register.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsEmptyAnchorIds()
    {
        var registry = new AutorefsRegistry();
        var html = "<p>before</p><a href=\"\" id=\"T:Akavache.IFoo\"></a>\n<h1 id=\"intro\">Intro</h1>"u8;
        HeadingIdScanner.ScanAndRegister(html, "api/foo.html"u8.ToArray(), registry);

        await Assert.That(registry.Count).IsEqualTo(2);
        await Assert.That(registry.TryResolve("T:Akavache.IFoo"u8, out var anchorUrl)).IsTrue();
        await Assert.That(anchorUrl.AsSpan().SequenceEqual("api/foo.html#T:Akavache.IFoo"u8)).IsTrue();
    }

    /// <summary>An anchor without an <c>id</c> attribute, and unrelated tags whose name starts with <c>a</c>, do not register.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IgnoresAnchorWithoutIdAndOtherATags()
    {
        var registry = new AutorefsRegistry();
        var html = "<a href=\"x\">link</a><abbr id=\"nope\">abbr</abbr><article id=\"alsoNope\">x</article>"u8;
        HeadingIdScanner.ScanAndRegister(html, "p.html"u8.ToArray(), registry);
        await Assert.That(registry.Count).IsEqualTo(0);
    }

    /// <summary>HTML without any heading IDs leaves the registry untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsHeadingsWithoutId()
    {
        var registry = new AutorefsRegistry();
        var html = "<p>just a paragraph</p>"u8;
        HeadingIdScanner.ScanAndRegister(html, "page.html"u8.ToArray(), registry);
        await Assert.That(registry.Count).IsEqualTo(0);
    }
}
