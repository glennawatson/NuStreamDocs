// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>Behavior tests for <c>MaterialThemeOptionsExtensions</c>.</summary>
public class MaterialThemeOptionsExtensionsTests
{
    /// <summary>Gets the fixture CDN root.</summary>
    private static ReadOnlySpan<byte> CdnRootBytes => "https://cdn.example"u8;

    /// <summary>Gets the fixture asset root.</summary>
    private static ReadOnlySpan<byte> AssetRootBytes => "/assets"u8;

    /// <summary>Gets the repository edit path.</summary>
    private static ReadOnlySpan<byte> EditPrefixBytes => "edit/main/docs"u8;

    /// <summary>Gets the fixture repository URL.</summary>
    private static ReadOnlySpan<byte> RepositoryUrlBytes => "https://github.com/owner/repo"u8;

    /// <summary>Gets the fixture copyright text.</summary>
    private static ReadOnlySpan<byte> CopyrightTextBytes => "(c) 2026"u8;

    /// <summary>Gets the fixture language.</summary>
    private static ReadOnlySpan<byte> LanguageBytes => "en-GB"u8;

    /// <summary>Gets the fixture site URL.</summary>
    private static ReadOnlySpan<byte> SiteUrlBytes => "https://example.test"u8;

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
        await Assert.That(updated.SiteUrl.AsSpan().SequenceEqual(SiteUrlBytes)).IsTrue();
        await Assert.That(updated.Language.AsSpan().SequenceEqual(LanguageBytes)).IsTrue();
        await Assert.That(updated.Copyright.AsSpan().SequenceEqual(CopyrightTextBytes)).IsTrue();
        await Assert.That(updated.RepoUrl.AsSpan().SequenceEqual(RepositoryUrlBytes)).IsTrue();
        await Assert.That(updated.EditUri.AsSpan().SequenceEqual(EditPrefixBytes)).IsTrue();
        await Assert.That(updated.EmbeddedAssetRoot.AsSpan().SequenceEqual(AssetRootBytes)).IsTrue();
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
        await Assert.That(d.EmbeddedAssetRoot.AsSpan().SequenceEqual(AssetRootBytes)).IsTrue();
    }

    /// <summary><see cref="MaterialThemeOptions.ResolveAssetRoot"/> picks between CDN and embedded based on <c>AssetSource</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolveAssetRootSwitchesByAssetSource()
    {
        var embedded = MaterialThemeOptions.Default;
        await Assert.That(embedded.ResolveAssetRoot().SequenceEqual(AssetRootBytes)).IsTrue();

        var cdn = embedded.WithCdnRoot("https://cdn.example") with { AssetSource = MaterialAssetSource.Cdn };
        await Assert.That(cdn.ResolveAssetRoot().SequenceEqual(CdnRootBytes)).IsTrue();
    }

    /// <summary>The <see cref="ReadOnlySpan{T}"/> overloads accept <c>"..."u8</c> literals directly and copy the bytes into the option's storage.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadsAcceptU8LiteralsDirectly()
    {
        var updated = MaterialThemeOptions.Default
            .WithSiteName("Site"u8)
            .WithSiteUrl(SiteUrlBytes)
            .WithLanguage(LanguageBytes)
            .WithCopyright(CopyrightTextBytes)
            .WithRepoUrl(RepositoryUrlBytes)
            .WithEditUri(EditPrefixBytes)
            .WithEmbeddedAssetRoot(AssetRootBytes)
            .WithCdnRoot(CdnRootBytes);

        await Assert.That(updated.SiteName.AsSpan().SequenceEqual("Site"u8)).IsTrue();
        await Assert.That(updated.SiteUrl.AsSpan().SequenceEqual(SiteUrlBytes)).IsTrue();
        await Assert.That(updated.Language.AsSpan().SequenceEqual(LanguageBytes)).IsTrue();
        await Assert.That(updated.Copyright.AsSpan().SequenceEqual(CopyrightTextBytes)).IsTrue();
        await Assert.That(updated.RepoUrl.AsSpan().SequenceEqual(RepositoryUrlBytes)).IsTrue();
        await Assert.That(updated.EditUri.AsSpan().SequenceEqual(EditPrefixBytes)).IsTrue();
        await Assert.That(updated.EmbeddedAssetRoot.AsSpan().SequenceEqual(AssetRootBytes)).IsTrue();
        await Assert.That(updated.CdnRoot.AsSpan().SequenceEqual(CdnRootBytes)).IsTrue();
    }
}
