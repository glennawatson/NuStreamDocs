// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Icons.MaterialDesign.Tests;

/// <summary>End-to-end coverage for the MDI lookup, resolver, and rewriter integration.</summary>
public class MdiIconLookupTests
{
    /// <summary>Builder produces a lookup whose <c>TryGet</c> returns the SVG bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuilderRoundTripsSvg()
    {
        var lookup = new MdiIconLookupBuilder()
            .Add("rocket-launch", "<svg>R</svg>"u8)
            .Add("source-branch", "<svg>S</svg>"u8)
            .Build();

        await Assert.That(lookup.Count).IsEqualTo(2);
        await Assert.That(Resolve(lookup, "rocket-launch")).IsEqualTo("<svg>R</svg>");
        await Assert.That(Resolve(lookup, "source-branch")).IsEqualTo("<svg>S</svg>");
    }

    /// <summary>Unknown names miss cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownNameMisses()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "<svg/>"u8).Build();
        await Assert.That(Resolve(lookup, "no-such-icon")).IsEqualTo(string.Empty);
    }

    /// <summary>Lookup is case-sensitive — MDI names are kebab-lowercase by spec.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LookupIsCaseSensitive()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "<svg/>"u8).Build();
        await Assert.That(Resolve(lookup, "Rocket-Launch")).IsEqualTo(string.Empty);
    }

    /// <summary>Resolver implements <see cref="IIconResolver"/> and proxies the lookup.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolverProxiesLookup()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "<svg>R</svg>"u8).Build();
        IIconResolver resolver = new MdiIconResolver(lookup);

        var foundHit = TryResolveString(resolver, "rocket-launch", out var hitSvg);
        var foundMiss = TryResolveString(resolver, "missing", out _);

        await Assert.That(foundHit).IsTrue();
        await Assert.That(hitSvg).IsEqualTo("<svg>R</svg>");
        await Assert.That(foundMiss).IsFalse();
    }

    /// <summary>Rewriter inlines the resolved SVG instead of emitting a font-ligature span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriterInlinesResolvedSvg()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "<svg viewBox=\"0 0 24 24\"/>"u8).Build();
        var resolver = new MdiIconResolver(lookup);
        var sink = new ArrayBufferWriter<byte>(64);

        IconShortcodeRewriter.Rewrite(
            "Click :material-rocket-launch: now"u8,
            sink,
            "material-symbols-outlined"u8,
            resolver);

        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).IsEqualTo("Click <svg viewBox=\"0 0 24 24\"/> now");
    }

    /// <summary>When the resolver misses, the rewriter falls back to the font-ligature span shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnresolvedFallsBackToFontLigature()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "<svg/>"u8).Build();
        var resolver = new MdiIconResolver(lookup);
        var sink = new ArrayBufferWriter<byte>(64);

        IconShortcodeRewriter.Rewrite(
            "Use :material-not-in-bundle: here"u8,
            sink,
            "material-symbols-outlined"u8,
            resolver);

        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).IsEqualTo("Use <span class=\"material-symbols-outlined\">not_in_bundle</span> here");
    }

    /// <summary>Default bundle (when no embedded blob is present) hands out an empty lookup so resolvers fall through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultBundleIsEmptyWhenResourceIsMissing()
    {
        // Initial commit ships no blob; this test pins the contract so we notice if a future stub
        // accidentally seeds entries the build doesn't actually have.
        var bundle = MdiIconBundle.Default;
        await Assert.That(bundle.Count).IsEqualTo(0);
    }

    /// <summary>Resolves <paramref name="name"/> against <paramref name="lookup"/> and decodes the result before any await.</summary>
    /// <param name="lookup">Lookup under test.</param>
    /// <param name="name">UTF-8 icon name.</param>
    /// <returns>Decoded SVG (empty when missed).</returns>
    private static string Resolve(MdiIconLookup lookup, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        return lookup.TryGet(bytes, out var svg) ? Encoding.UTF8.GetString(svg) : string.Empty;
    }

    /// <summary>Routes a name through <paramref name="resolver"/> and returns the decoded SVG, materialising before the caller awaits.</summary>
    /// <param name="resolver">Resolver under test.</param>
    /// <param name="name">UTF-8 icon name.</param>
    /// <param name="svg">Decoded SVG on hit.</param>
    /// <returns>Hit flag.</returns>
    private static bool TryResolveString(IIconResolver resolver, string name, out string svg)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        if (resolver.TryResolve(bytes, out var resolved))
        {
            svg = Encoding.UTF8.GetString(resolved);
            return true;
        }

        svg = string.Empty;
        return false;
    }
}
