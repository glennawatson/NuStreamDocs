// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Social.Tests;

/// <summary>Unit tests for <see cref="SocialOptions"/> and <see cref="SocialOptionsExtensions"/>.</summary>
public sealed class SocialOptionsTests
{
    /// <summary>Verifies default property values on <see cref="SocialOptions.Default"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Default_PopulatesExpectedFlagsAndColors()
    {
        var defaults = SocialOptions.Default;

        await Assert.That(defaults.Enabled).IsTrue();
        await Assert.That(defaults.EmitOpenGraph).IsTrue();
        await Assert.That(defaults.EmitTwitterCard).IsTrue();
        await Assert.That(defaults.EmitJsonLd).IsTrue();
        await Assert.That(defaults.EmitCanonicalLink).IsTrue();
        await Assert.That(defaults.GenerateSvgCards).IsTrue();
        await Assert.That(defaults.TwitterSite.Length).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(defaults.AccentColor)).IsEqualTo("#2563eb");
        await Assert.That(Encoding.UTF8.GetString(defaults.BackgroundColor)).IsEqualTo("#0f172a");
    }

    /// <summary>Verifies every fluent option extension mutates the expected setting cleanly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FluentExtensions_UpdateIndividualOptions()
    {
        var customized = SocialOptions.Default
            .WithTwitterSite("@nustream"u8)
            .WithCardColors("#111827"u8, "#38bdf8"u8)
            .WithoutSvgCards()
            .WithoutJsonLd()
            .WithoutTwitterCard()
            .WithoutOpenGraph()
            .WithoutCanonicalLink()
            .Disable();

        await Assert.That(customized.Enabled).IsFalse();
        await Assert.That(customized.GenerateSvgCards).IsFalse();
        await Assert.That(customized.EmitJsonLd).IsFalse();
        await Assert.That(customized.EmitTwitterCard).IsFalse();
        await Assert.That(customized.EmitOpenGraph).IsFalse();
        await Assert.That(customized.EmitCanonicalLink).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(customized.TwitterSite)).IsEqualTo("@nustream");
        await Assert.That(Encoding.UTF8.GetString(customized.BackgroundColor)).IsEqualTo("#111827");
        await Assert.That(Encoding.UTF8.GetString(customized.AccentColor)).IsEqualTo("#38bdf8");
    }
}
