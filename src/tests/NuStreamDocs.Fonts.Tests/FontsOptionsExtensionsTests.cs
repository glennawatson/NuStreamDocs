// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <c>FontsOptionsExtensions</c>.</summary>
public class FontsOptionsExtensionsTests
{
    /// <summary><c>AddGoogleFont</c> (minimal) appends a face with sane defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddGoogleFontMinimalDefaults()
    {
        var o = FontsOptions.Default.AddGoogleFont("Source Sans 3"u8, 400, 700);
        await Assert.That(o.Faces.Length).IsEqualTo(1);
        var f = o.Faces[0];
        await Assert.That(Encoding.UTF8.GetString(f.FamilyBytes)).IsEqualTo("Source Sans 3");
        await Assert.That(Encoding.UTF8.GetString(f.Id)).IsEqualTo("source-sans-3");
        await Assert.That(f.Provider).IsEqualTo(FontProviderKind.Google);
        await Assert.That(f.Weights.SequenceEqual([400, 700])).IsTrue();
        await Assert.That(f.Styles.SequenceEqual([FontStyle.Normal])).IsTrue();
        await Assert.That(f.Subsets.Length).IsEqualTo(2);
        await Assert.That(f.Display).IsEqualTo(FontDisplay.Swap);
        await Assert.That(f.Preload).IsTrue();
    }

    /// <summary>An empty weights array falls back to weight 400.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddGoogleFontEmptyWeightsDefaultsTo400()
    {
        var o = FontsOptions.Default.AddGoogleFont("Inter"u8);
        await Assert.That(o.Faces[0].Weights.SequenceEqual([400])).IsTrue();
    }

    /// <summary><c>AddFace</c> stores a fully specified face verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddFaceStoresVerbatim()
    {
        var face = new FontFace(
            [.. "jetbrains-mono"u8],
            [.. "JetBrains Mono"u8],
            FontProviderKind.Fontsource,
            [400, 700],
            [FontStyle.Normal],
            [[.. "latin"u8]],
            FontDisplay.Optional,
            false,
            GenericFontFamily.Monospace,
            [],
            [[.. "--md-code-font"u8]]);
        var o = FontsOptions.Default.AddFace(face);
        var f = o.Faces[0];
        await Assert.That(f.Provider).IsEqualTo(FontProviderKind.Fontsource);
        await Assert.That(f.Display).IsEqualTo(FontDisplay.Optional);
        await Assert.That(f.Preload).IsFalse();
        await Assert.That(f.Fallback).IsEqualTo(GenericFontFamily.Monospace);
        await Assert.That(f.Subsets.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(f.ThemeVariables[0])).IsEqualTo("--md-code-font");
    }

    /// <summary><c>AddLocalFont</c> records the glob patterns and marks the provider local.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddLocalFontRecordsGlobs()
    {
        var o = FontsOptions.Default.AddLocalFont("MyFont"u8, "fonts/MyFont-*.woff2");
        var f = o.Faces[0];
        await Assert.That(f.Provider).IsEqualTo(FontProviderKind.Local);
        await Assert.That(f.LocalSrc.Length).IsEqualTo(1);
        await Assert.That(f.LocalSrc[0].Value).IsEqualTo("fonts/MyFont-*.woff2");
    }

    /// <summary>Global option setters round-trip; <c>ClearFaces</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GlobalSettersAndClear()
    {
        var o = FontsOptions.Default
            .AddGoogleFont("Inter"u8)
            .WithOffline(true)
            .WithOutputSubdirectory("assets/webfonts");
        await Assert.That(o.Offline).IsTrue();
        await Assert.That(o.OutputSubdirectory.Value).IsEqualTo("assets/webfonts");
        await Assert.That(o.ClearFaces().Faces.Length).IsEqualTo(0);
    }
}
