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
    /// <summary>Builder produces a lookup whose <c>TryGet</c> returns the path-data bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuilderRoundTripsPathData()
    {
        var lookup = new MdiIconLookupBuilder()
            .Add("rocket-launch", "M1,1H2"u8)
            .Add("source-branch", "M3,3H4"u8)
            .Build();

        await Assert.That(lookup.Count).IsEqualTo(2);
        await Assert.That(Resolve(lookup, "rocket-launch")).IsEqualTo("M1,1H2");
        await Assert.That(Resolve(lookup, "source-branch")).IsEqualTo("M3,3H4");
    }

    /// <summary>Unknown names miss cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownNameMisses()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "M1H2"u8).Build();
        await Assert.That(Resolve(lookup, "no-such-icon")).IsEqualTo(string.Empty);
    }

    /// <summary>Lookup is case-sensitive — MDI names are kebab-lowercase by spec.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LookupIsCaseSensitive()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "M1H2"u8).Build();
        await Assert.That(Resolve(lookup, "Rocket-Launch")).IsEqualTo(string.Empty);
    }

    /// <summary>Resolver wraps the path data in the standard SVG envelope.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolverWrapsPathDataInSvgEnvelope()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "M1,1H2"u8).Build();
        var resolver = new MdiIconResolver(lookup);

        var sink = new ArrayBufferWriter<byte>(128);
        var found = resolver.TryResolve("rocket-launch"u8, sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(found).IsTrue();
        await Assert.That(output).IsEqualTo(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path d=\"M1,1H2\"/></svg>");
    }

    /// <summary>Resolver returns false (and writes nothing) on a miss.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolverWritesNothingOnMiss()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "M1,1H2"u8).Build();
        var resolver = new MdiIconResolver(lookup);

        var sink = new ArrayBufferWriter<byte>(64);
        var found = resolver.TryResolve("missing"u8, sink);

        await Assert.That(found).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Rewriter inlines the wrapped SVG produced by the resolver.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriterInlinesResolvedSvg()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "M1,1H2"u8).Build();
        var resolver = new MdiIconResolver(lookup);
        var sink = new ArrayBufferWriter<byte>(128);

        IconShortcodeRewriter.Rewrite(
            "Click :material-rocket-launch: now"u8,
            sink,
            "material-symbols-outlined"u8,
            resolver);

        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).IsEqualTo(
            "Click <svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path d=\"M1,1H2\"/></svg> now");
    }

    /// <summary>When the resolver misses, the rewriter falls back to the font-ligature span shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnresolvedFallsBackToFontLigature()
    {
        var lookup = new MdiIconLookupBuilder().Add("rocket-launch", "M1,1H2"u8).Build();
        var resolver = new MdiIconResolver(lookup);
        var sink = new ArrayBufferWriter<byte>(128);

        IconShortcodeRewriter.Rewrite(
            "Use :material-not-in-bundle: here"u8,
            sink,
            "material-symbols-outlined"u8,
            resolver);

        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).IsEqualTo("Use <span class=\"material-symbols-outlined\">not_in_bundle</span> here");
    }

    /// <summary>Default bundle is populated with the embedded MDI catalogue.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultBundleHasFullMdiCatalogue() =>

        // Sanity floor — the upstream catalogue has been > 7000 icons since 2023; guards against
        // an empty / partial bundle slipping through on regen.
        await Assert.That(MdiIconBundle.Count).IsGreaterThan(6000);

    /// <summary>Every icon name rxui's docs use today resolves through the default bundle.</summary>
    /// <param name="iconName">MDI icon name (kebab-case, no <c>material-</c> prefix).</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("rocket-launch")]
    [Arguments("source-branch")]
    [Arguments("test-tube")]
    [Arguments("monitor-cellphone")]
    [Arguments("puzzle-outline")]
    [Arguments("script-text-outline")]
    [Arguments("book-open-page-variant-outline")]
    [Arguments("account-group-outline")]
    [Arguments("weather-night")]
    [Arguments("weather-sunny")]
    public Task RxuiUsedIconsResolveInDefaultBundle(string iconName) => AssertIconResolves(iconName);

    /// <summary>Common docs-site MDI staples resolve through the default bundle — guards against a regen accidentally dropping one.</summary>
    /// <param name="iconName">MDI icon name (kebab-case, no <c>material-</c> prefix).</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("account")]
    [Arguments("account-circle")]
    [Arguments("login")]
    [Arguments("logout")]
    [Arguments("alert")]
    [Arguments("alert-circle")]
    [Arguments("check")]
    [Arguments("check-circle")]
    [Arguments("close")]
    [Arguments("close-circle")]
    [Arguments("information")]
    [Arguments("information-outline")]
    [Arguments("help-circle")]
    [Arguments("arrow-up")]
    [Arguments("arrow-down")]
    [Arguments("arrow-left")]
    [Arguments("arrow-right")]
    [Arguments("chevron-up")]
    [Arguments("chevron-down")]
    [Arguments("chevron-left")]
    [Arguments("chevron-right")]
    [Arguments("menu")]
    [Arguments("home")]
    [Arguments("cog")]
    [Arguments("magnify")]
    [Arguments("dots-vertical")]
    [Arguments("pencil")]
    [Arguments("delete")]
    [Arguments("download")]
    [Arguments("upload")]
    [Arguments("share-variant")]
    [Arguments("content-copy")]
    [Arguments("folder")]
    [Arguments("folder-open")]
    [Arguments("file")]
    [Arguments("file-document")]
    [Arguments("calendar")]
    [Arguments("clock")]
    [Arguments("github")]
    [Arguments("code-tags")]
    [Arguments("bug")]
    [Arguments("lightbulb")]
    [Arguments("lock")]
    [Arguments("lock-open")]
    [Arguments("key")]
    public Task CommonIconsResolveInDefaultBundle(string iconName) => AssertIconResolves(iconName);

    /// <summary>Asserts <paramref name="iconName"/> resolves to a non-empty path-data span in the default bundle.</summary>
    /// <param name="iconName">UTF-8 icon name.</param>
    /// <returns>Async test.</returns>
    private static async Task AssertIconResolves(string iconName)
    {
        var bytes = Encoding.UTF8.GetBytes(iconName);
        var found = MdiIconBundle.TryGet(bytes, out var path);
        var nonEmpty = path.Length > 0;

        await Assert.That(found).IsTrue();
        await Assert.That(nonEmpty).IsTrue();
    }

    /// <summary>Resolves <paramref name="name"/> against <paramref name="lookup"/> and decodes the result before any await.</summary>
    /// <param name="lookup">Lookup under test.</param>
    /// <param name="name">UTF-8 icon name.</param>
    /// <returns>Decoded path data (empty when missed).</returns>
    private static string Resolve(MdiIconLookup lookup, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        return lookup.TryGet(bytes, out var path) ? Encoding.UTF8.GetString(path) : string.Empty;
    }
}
