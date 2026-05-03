// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Behavior tests for <c>Material3ThemeOptionsExtensions</c>.</summary>
public class Material3ThemeOptionsExtensionsTests
{
    /// <summary>String overloads encode to UTF-8 once at the boundary.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StringOverloadsEncodeUtf8()
    {
        var updated = Material3ThemeOptions.Default
            .WithSiteName("MD3 Site")
            .WithSiteUrl("https://md3.example")
            .WithLanguage("en")
            .WithCopyright("(c)")
            .WithRepoUrl("https://github.com/owner/repo")
            .WithEditUri("edit/main/docs")
            .WithEmbeddedAssetRoot("/assets")
            .WithCdnRoot("https://cdn.md3");

        await Assert.That(updated.SiteName.AsSpan().SequenceEqual("MD3 Site"u8)).IsTrue();
        await Assert.That(updated.SiteUrl.AsSpan().SequenceEqual("https://md3.example"u8)).IsTrue();
        await Assert.That(updated.Language.AsSpan().SequenceEqual("en"u8)).IsTrue();
        await Assert.That(updated.Copyright.AsSpan().SequenceEqual("(c)"u8)).IsTrue();
        await Assert.That(updated.RepoUrl.AsSpan().SequenceEqual("https://github.com/owner/repo"u8)).IsTrue();
        await Assert.That(updated.EditUri.AsSpan().SequenceEqual("edit/main/docs"u8)).IsTrue();
        await Assert.That(updated.EmbeddedAssetRoot.AsSpan().SequenceEqual("/assets"u8)).IsTrue();
        await Assert.That(updated.CdnRoot.AsSpan().SequenceEqual("https://cdn.md3"u8)).IsTrue();
    }

    /// <summary>Byte overloads store the supplied array verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadsStoreVerbatim()
    {
        byte[] siteName = [.. "Verbatim"u8];
        var updated = Material3ThemeOptions.Default.WithSiteName(siteName);
        await Assert.That(updated.SiteName).IsSameReferenceAs(siteName);
    }

    /// <summary>Defaults are byte-shaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsAreShapedCorrectly()
    {
        var d = Material3ThemeOptions.Default;
        await Assert.That(d.SiteName.Length).IsEqualTo(0);
        await Assert.That(d.SiteUrl.Length).IsEqualTo(0);
        await Assert.That(d.Language.AsSpan().SequenceEqual("en"u8)).IsTrue();
        await Assert.That(d.EmbeddedAssetRoot.AsSpan().SequenceEqual("/assets"u8)).IsTrue();
        await Assert.That(d.CdnRoot.Length).IsEqualTo(0);
    }

    /// <summary><see cref="Material3ThemeOptions.ResolveAssetRoot"/> switches by <c>AssetSource</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolveAssetRootSwitchesByAssetSource()
    {
        var embedded = Material3ThemeOptions.Default;
        await Assert.That(embedded.ResolveAssetRoot().AsSpan().SequenceEqual("/assets"u8)).IsTrue();

        var cdn = embedded.WithCdnRoot("https://cdn.md3.example") with { AssetSource = Material3AssetSource.Cdn };
        await Assert.That(cdn.ResolveAssetRoot().AsSpan().SequenceEqual("https://cdn.md3.example"u8)).IsTrue();
    }

    /// <summary>Null byte overloads throw.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullByteOverloadsThrow()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => Material3ThemeOptions.Default.WithSiteName((byte[])null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>The <see cref="ReadOnlySpan{T}"/> overloads accept <c>"..."u8</c> literals directly and copy the bytes into the option's storage.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadsAcceptU8LiteralsDirectly()
    {
        var updated = Material3ThemeOptions.Default
            .WithSiteName("MD3"u8)
            .WithSiteUrl("https://md3.example"u8)
            .WithLanguage("en"u8)
            .WithCopyright("(c)"u8)
            .WithRepoUrl("https://github.com/o/r"u8)
            .WithEditUri("edit/main/docs"u8)
            .WithEmbeddedAssetRoot("/assets"u8)
            .WithCdnRoot("https://cdn.md3"u8);

        await Assert.That(updated.SiteName.AsSpan().SequenceEqual("MD3"u8)).IsTrue();
        await Assert.That(updated.SiteUrl.AsSpan().SequenceEqual("https://md3.example"u8)).IsTrue();
        await Assert.That(updated.Language.AsSpan().SequenceEqual("en"u8)).IsTrue();
        await Assert.That(updated.Copyright.AsSpan().SequenceEqual("(c)"u8)).IsTrue();
        await Assert.That(updated.RepoUrl.AsSpan().SequenceEqual("https://github.com/o/r"u8)).IsTrue();
        await Assert.That(updated.EditUri.AsSpan().SequenceEqual("edit/main/docs"u8)).IsTrue();
        await Assert.That(updated.EmbeddedAssetRoot.AsSpan().SequenceEqual("/assets"u8)).IsTrue();
        await Assert.That(updated.CdnRoot.AsSpan().SequenceEqual("https://cdn.md3"u8)).IsTrue();
    }
}
