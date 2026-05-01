// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>Behaviour tests for <c>LinkExtractor</c>.</summary>
public class LinkExtractorTests
{
    /// <summary>Every <c>href</c> attribute on the page is captured.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsEveryHref()
    {
        var html = "<a href=\"a.html\">a</a> <a href=\"https://example.com\">b</a>"u8;
        var hrefs = LinkExtractor.ExtractHrefs(html);
        await Assert.That(hrefs.Length).IsEqualTo(2);
        await Assert.That(hrefs[0]).IsEqualTo("a.html");
        await Assert.That(hrefs[1]).IsEqualTo("https://example.com");
    }

    /// <summary>Heading <c>id</c> attributes are captured but non-heading id attrs are skipped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsHeadingIdsOnly()
    {
        var html = "<h1 id=\"intro\">Intro</h1><p id=\"para\">body</p><h2 id=\"detail\">x</h2>"u8;
        var ids = LinkExtractor.ExtractHeadingIds(html);
        await Assert.That(ids.Length).IsEqualTo(2);
        await Assert.That(ids[0]).IsEqualTo("intro");
        await Assert.That(ids[1]).IsEqualTo("detail");
    }
}
