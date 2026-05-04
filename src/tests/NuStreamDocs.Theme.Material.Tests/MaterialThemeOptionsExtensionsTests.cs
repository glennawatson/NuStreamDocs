// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>Behavior tests for <c>MaterialThemeOptionsExtensions</c>.</summary>
public class MaterialThemeOptionsExtensionsTests
{
    /// <summary>String overloads encode to UTF-8 once at the boundary.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StringOverloadsEncodeUtf8()
    {
        var updated = MaterialThemeOptions.Default
            .WithSiteName("Site")
            .WithSiteUrl("https://example.test")
            .WithLanguage("en-GB")
            .WithCopyright("(c) 2026")
            .WithRepoUrl("https://github.com/owner/repo")
            .WithEditUri("edit/main/docs")
            .WithEmbeddedAssetRoot("/assets")
            .WithCdnRoot("https://cdn.example.test");

        await Assert.That(updated.SiteName.AsSpan().SequenceEqual("Site"u8)).IsTrue();
        await Assert.That(updated.SiteUrl.AsSpan().SequenceEqual("https://example.test"u8)).IsTrue();
        await Assert.That(updated.Language.AsSpan().SequenceEqual("en-GB"u8)).IsTrue();
        await Assert.That(updated.Copyright.AsSpan().SequenceEqual("(c) 2026"u8)).IsTrue();
        await Assert.That(updated.RepoUrl.AsSpan().SequenceEqual("https://github.com/owner/repo"u8)).IsTrue();
        await Assert.That(updated.EditUri.AsSpan().SequenceEqual("edit/main/docs"u8)).IsTrue();
        await Assert.That(updated.EmbeddedAssetRoot.AsSpan().SequenceEqual("/assets"u8)).IsTrue();
        await Assert.That(updated.CdnRoot.AsSpan().SequenceEqual("https://cdn.example.test"u8)).IsTrue();
    }

    /// <summary>Empty/null strings encode to an empty byte array (matches <c>Utf8Encoder.Encode</c>).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyStringMapsToEmptyBytes()
    {
        var updated = MaterialThemeOptions.Default
            .WithSiteName(string.Empty)
            .WithCopyright(string.Empty);
        await Assert.That(updated.SiteName.Length).IsEqualTo(0);
        await Assert.That(updated.Copyright.Length).IsEqualTo(0);
    }

    /// <summary>Byte overloads store the supplied array verbatim — no encode round-trip.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadsStoreVerbatim()
    {
        byte[] siteName = [.. "Bytes"u8];
        byte[] siteUrl = [.. "https://b.example"u8];
        byte[] cdnRoot = [.. "https://cdn.b"u8];

        var updated = MaterialThemeOptions.Default
            .WithSiteName(siteName)
            .WithSiteUrl(siteUrl)
            .WithCdnRoot(cdnRoot);

        await Assert.That(updated.SiteName).IsSameReferenceAs(siteName);
        await Assert.That(updated.SiteUrl).IsSameReferenceAs(siteUrl);
        await Assert.That(updated.CdnRoot).IsSameReferenceAs(cdnRoot);
    }

    /// <summary>Defaults preserve sane shapes for the byte fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsAreShapedCorrectly()
    {
        var d = MaterialThemeOptions.Default;
        await Assert.That(d.SiteName.Length).IsEqualTo(0);
        await Assert.That(d.SiteUrl.Length).IsEqualTo(0);
        await Assert.That(d.Language.AsSpan().SequenceEqual("en"u8)).IsTrue();
        await Assert.That(d.Copyright.Length).IsEqualTo(0);
        await Assert.That(d.RepoUrl.Length).IsEqualTo(0);
        await Assert.That(d.EditUri.Length).IsEqualTo(0);
        await Assert.That(d.EmbeddedAssetRoot.AsSpan().SequenceEqual("/assets"u8)).IsTrue();
    }

    /// <summary><see cref="MaterialThemeOptions.ResolveAssetRoot"/> picks between CDN and embedded based on <c>AssetSource</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolveAssetRootSwitchesByAssetSource()
    {
        var embedded = MaterialThemeOptions.Default;
        await Assert.That(embedded.ResolveAssetRoot().SequenceEqual("/assets"u8)).IsTrue();

        var cdn = embedded.WithCdnRoot("https://cdn.example") with { AssetSource = MaterialAssetSource.Cdn };
        await Assert.That(cdn.ResolveAssetRoot().SequenceEqual("https://cdn.example"u8)).IsTrue();
    }

    /// <summary>Null byte overloads throw.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullByteOverloadsThrow()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => MaterialThemeOptions.Default.WithSiteName((byte[])null!));
        var ex2 = Assert.Throws<ArgumentNullException>(static () => MaterialThemeOptions.Default.WithCdnRoot((byte[])null!));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
    }

    /// <summary>The <see cref="ReadOnlySpan{T}"/> overloads accept <c>"..."u8</c> literals directly and copy the bytes into the option's storage.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadsAcceptU8LiteralsDirectly()
    {
        var updated = MaterialThemeOptions.Default
            .WithSiteName("Site"u8)
            .WithSiteUrl("https://example.test"u8)
            .WithLanguage("en-GB"u8)
            .WithCopyright("(c) 2026"u8)
            .WithRepoUrl("https://github.com/owner/repo"u8)
            .WithEditUri("edit/main/docs"u8)
            .WithEmbeddedAssetRoot("/assets"u8)
            .WithCdnRoot("https://cdn.example"u8);

        await Assert.That(updated.SiteName.AsSpan().SequenceEqual("Site"u8)).IsTrue();
        await Assert.That(updated.SiteUrl.AsSpan().SequenceEqual("https://example.test"u8)).IsTrue();
        await Assert.That(updated.Language.AsSpan().SequenceEqual("en-GB"u8)).IsTrue();
        await Assert.That(updated.Copyright.AsSpan().SequenceEqual("(c) 2026"u8)).IsTrue();
        await Assert.That(updated.RepoUrl.AsSpan().SequenceEqual("https://github.com/owner/repo"u8)).IsTrue();
        await Assert.That(updated.EditUri.AsSpan().SequenceEqual("edit/main/docs"u8)).IsTrue();
        await Assert.That(updated.EmbeddedAssetRoot.AsSpan().SequenceEqual("/assets"u8)).IsTrue();
        await Assert.That(updated.CdnRoot.AsSpan().SequenceEqual("https://cdn.example"u8)).IsTrue();
    }
}
