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
    /// <summary>Expected icon count.</summary>
    private const int ExpectedIconCount = 2;

    /// <summary>Rocket icon name.</summary>
    private const string RocketIconName = "rocket-launch";

    /// <summary>Name of the source-control branch icon.</summary>
    private const string BranchIconName = "source-branch";

    /// <summary>Rocket path data.</summary>
    private const string RocketPathData = "M1,1H2";

    /// <summary>Initial output capacity.</summary>
    private const int InitialOutputCapacity = 128;

    /// <summary>Missing output capacity.</summary>
    private const int MissingOutputCapacity = 64;

    /// <summary>Minimum bundle count.</summary>
    private const int MinimumBundleCount = 6000;

    /// <summary>Gets the rocket icon name bytes.</summary>
    private static ReadOnlySpan<byte> RocketIconNameBytes => "rocket-launch"u8;

    /// <summary>Gets the rocket path data bytes.</summary>
    private static ReadOnlySpan<byte> RocketPathDataBytes => "M1,1H2"u8;

    /// <summary>Supplies documentation icon names required in the bundle.</summary>
    /// <returns>Names exercised by the bundle lookup test.</returns>
    public static IEnumerable<string> IconNames() =>
    [
        RocketIconName, BranchIconName, "test-tube", "monitor-cellphone",
        "puzzle-outline", "script-text-outline", "book-open-page-variant-outline", "account-group-outline",
        "weather-night", "weather-sunny", "account", "account-circle",
        "login", "logout", "alert", "alert-circle",
        "check", "check-circle", "close", "close-circle",
        "information", "information-outline", "help-circle", "arrow-up",
        "arrow-down", "arrow-left", "arrow-right", "chevron-up",
        "chevron-down", "chevron-left", "chevron-right", "menu",
        "home", "cog", "magnify", "dots-vertical",
        "pencil", "delete", "download", "upload",
        "share-variant", "content-copy", "folder", "folder-open",
        "file", "file-document", "calendar", "clock",
        "github", "code-tags", "bug", "lightbulb",
        "lock", "lock-open", "key"
    ];

    /// <summary>Builder produces a lookup whose <c>TryGet</c> returns the path-data bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuilderRoundTripsPathData()
    {
        var lookup = new MdiIconLookupBuilder()
            .Add(RocketIconNameBytes, RocketPathDataBytes)
            .Add("source-branch"u8, "M3,3H4"u8)
            .Build();

        await Assert.That(lookup.Count).IsEqualTo(ExpectedIconCount);
        await Assert.That(Resolve(lookup, RocketIconName)).IsEqualTo(RocketPathData);
        await Assert.That(Resolve(lookup, BranchIconName)).IsEqualTo("M3,3H4");
    }

    /// <summary>Unknown names miss cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownNameMisses()
    {
        var lookup = new MdiIconLookupBuilder().Add(RocketIconNameBytes, "M1H2"u8).Build();
        await Assert.That(Resolve(lookup, "no-such-icon")).IsEqualTo(string.Empty);
    }

    /// <summary>Lookup is case-sensitive — MDI names are kebab-lowercase by spec.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LookupIsCaseSensitive()
    {
        var lookup = new MdiIconLookupBuilder().Add(RocketIconNameBytes, "M1H2"u8).Build();
        await Assert.That(Resolve(lookup, "Rocket-Launch")).IsEqualTo(string.Empty);
    }

    /// <summary>Resolver wraps the path data in the standard SVG envelope.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolverWrapsPathDataInSvgEnvelope()
    {
        var lookup = new MdiIconLookupBuilder().Add(RocketIconNameBytes, RocketPathDataBytes).Build();
        MdiIconResolver resolver = new(lookup);

        ArrayBufferWriter<byte> sink = new(InitialOutputCapacity);
        var found = resolver.TryResolve(RocketIconNameBytes, sink);
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
        var lookup = new MdiIconLookupBuilder().Add(RocketIconNameBytes, RocketPathDataBytes).Build();
        MdiIconResolver resolver = new(lookup);

        ArrayBufferWriter<byte> sink = new(MissingOutputCapacity);
        var found = resolver.TryResolve("missing"u8, sink);

        await Assert.That(found).IsFalse();
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Rewriter inlines the wrapped SVG produced by the resolver.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriterInlinesResolvedSvg()
    {
        var lookup = new MdiIconLookupBuilder().Add(RocketIconNameBytes, RocketPathDataBytes).Build();
        MdiIconResolver resolver = new(lookup);
        ArrayBufferWriter<byte> sink = new(InitialOutputCapacity);

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
        var lookup = new MdiIconLookupBuilder().Add(RocketIconNameBytes, RocketPathDataBytes).Build();
        MdiIconResolver resolver = new(lookup);
        ArrayBufferWriter<byte> sink = new(InitialOutputCapacity);

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
        await Assert.That(MdiIconBundle.Count).IsGreaterThan(MinimumBundleCount);

    /// <summary>Documentation icons resolve through the default bundle.</summary>
    /// <param name="iconName">MDI icon name (kebab-case, no <c>material-</c> prefix).</param>
    /// <returns>Async test.</returns>
    [Test]
    [MethodDataSource(nameof(IconNames))]
    public async Task IconsResolveInDefaultBundle(string iconName)
    {
        var bytes = Encoding.UTF8.GetBytes(iconName);
        var found = MdiIconBundle.TryGet(bytes, out var path);
        var nonEmpty = !path.IsEmpty;

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
