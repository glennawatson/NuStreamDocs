// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Direct tests for the per-shape <c>TryRewriteAt</c> / <c>TryRewriteBlock</c> internals exposed for the combined-walker dispatch path.</summary>
public class UrlRewriteAtTests
{
    /// <summary>AssetAttributeBytes.TryRewriteAt rewrites a matched <c>src=</c> at the candidate position.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AssetAttributeRewritesSrc()
    {
        byte[] html = [.. "<img src=\"https://cdn.test/a.png\">"u8];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(html.Length);

        // Candidate position is the 's' in "src=" inside the tag — index 5.
        var lastEmit = 0;
        var changed = AssetAttributeBytes.TryRewriteAt(html, 5, ctx, sink, ref lastEmit, out var advanceTo);

        await Assert.That(changed).IsTrue();
        await Assert.That(advanceTo).IsGreaterThan(5);
        await Assert.That(lastEmit).IsGreaterThan(5);
        var written = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(written).Contains("/local/");
    }

    /// <summary>AssetAttributeBytes.TryRewriteAt advances by one without writing when the candidate isn't an attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AssetAttributeMissAdvancesOneByte()
    {
        byte[] html = [.. "hello world"u8];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(html.Length);
        var lastEmit = 0;

        // Index 6 is 'w' in "world" — definitely not src/href.
        var changed = AssetAttributeBytes.TryRewriteAt(html, 6, ctx, sink, ref lastEmit, out var advanceTo);

        await Assert.That(changed).IsFalse();
        await Assert.That(advanceTo).IsEqualTo(7);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
        await Assert.That(lastEmit).IsEqualTo(0);
    }

    /// <summary>SrcsetBytes.TryRewriteAt rewrites the URL portion of a matched srcset attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SrcsetRewritesAttributeValue()
    {
        byte[] html = [.. "<img srcset=\"https://cdn.test/a.png 2x\">"u8];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(html.Length);
        var lastEmit = 0;

        // Index 5 is the 's' of 'srcset='.
        var changed = SrcsetBytes.TryRewriteAt(html, 5, ctx, sink, ref lastEmit, out var advanceTo);

        await Assert.That(changed).IsTrue();
        await Assert.That(advanceTo).IsGreaterThan(5);
        var written = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(written).Contains("/local/");
        await Assert.That(written).Contains(" 2x");
    }

    /// <summary>SrcsetBytes.TryRewriteAt returns false without writing when the candidate isn't a srcset attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SrcsetMissDoesNotWrite()
    {
        byte[] html = [.. "<img src=\"https://cdn.test/a.png\">"u8];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(html.Length);
        var lastEmit = 0;

        // Index 5 is 's' of 'src=' — not srcset.
        var changed = SrcsetBytes.TryRewriteAt(html, 5, ctx, sink, ref lastEmit, out var advanceTo);

        await Assert.That(changed).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
        await Assert.That(lastEmit).IsEqualTo(0);
        await Assert.That(advanceTo).IsEqualTo(6);
    }

    /// <summary>InlineStyleBlockBytes.TryRewriteBlock rewrites url() tokens inside a style block at the candidate position.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineStyleBlockRewritesUrlsInBody()
    {
        byte[] html = [.. "<style>.x { background: url(https://cdn.test/a.png); }</style>"u8];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(html.Length);
        var lastEmit = 0;

        // Index 0 is the '<' of '<style>'.
        var changed = InlineStyleBlockBytes.TryRewriteBlock(html, 0, ctx, sink, ref lastEmit, out var advanceTo);

        await Assert.That(changed).IsTrue();
        await Assert.That(advanceTo).IsEqualTo(html.Length);
        var written = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(written).Contains("url(/local/");
    }

    /// <summary>InlineStyleBlockBytes.TryRewriteBlock returns false on a non-style <c>&lt;</c> candidate.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineStyleBlockMissOnNonStyleTag()
    {
        byte[] html = [.. "<div>plain</div>"u8];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(html.Length);
        var lastEmit = 0;

        var changed = InlineStyleBlockBytes.TryRewriteBlock(html, 0, ctx, sink, ref lastEmit, out var advanceTo);

        await Assert.That(changed).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
        await Assert.That(lastEmit).IsEqualTo(0);
        await Assert.That(advanceTo).IsEqualTo(1);
    }

    /// <summary>The combined ExternalUrlScanner.RewriteInto handles all three URL shapes in a single pass over a mixed page.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CombinedWalkerHandlesAllThreeShapes()
    {
        const string Html = "<img src=\"https://cdn.test/a.png\">"
            + "<img srcset=\"https://cdn.test/b.png 2x\">"
            + "<style>.x { background: url(https://cdn.test/c.png); }</style>"
            + "<a href=\"https://cdn.test/page\">link</a>";
        byte[] bytes = [.. Encoding.UTF8.GetBytes(Html)];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(bytes.Length);

        var changed = ExternalUrlScanner.RewriteInto(bytes, ctx, sink);

        await Assert.That(changed).IsTrue();
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("src=\"/local/");
        await Assert.That(output).Contains("srcset=\"/local/");
        await Assert.That(output).Contains("url(/local/");
        await Assert.That(output).Contains("href=\"/local/");
    }

    /// <summary>The combined walker emits no bytes and returns false when no URL is localized.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CombinedWalkerNoMatchReturnsFalse()
    {
        byte[] html = [.. "<p>plain text with no urls</p>"u8];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: null);
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(html.Length);

        var changed = ExternalUrlScanner.RewriteInto(html, ctx, sink);

        await Assert.That(changed).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>The combined walker leaves filter-rejected URLs untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CombinedWalkerRespectsFilter()
    {
        byte[] html = [.. "<img src=\"https://cdn.test/a.png\">"u8];
        var registry = new ExternalAssetRegistry("local"u8.ToArray());
        var filter = new HostFilter(hostsToSkip: null, hostsAllowed: PrivacyTestHelpers.Utf8("only.test"));
        var ctx = new UrlRewriteContext(filter, registry);
        var sink = new ArrayBufferWriter<byte>(html.Length);

        var changed = ExternalUrlScanner.RewriteInto(html, ctx, sink);

        await Assert.That(changed).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }
}
