// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Theme.Material3;

/// <summary>String construction helpers for the byte-shaped <see cref="Material3ThemeOptions"/> record.</summary>
/// <remarks>
/// Encodes the inputs once at construction so the page-shell template flows pure UTF-8 from
/// configure through every render. Callers building from configuration files (which produce
/// strings) reach for these helpers; callers with byte-literal sources construct the record
/// directly with <c>"..."u8.ToArray()</c>.
/// </remarks>
public static class Material3ThemeOptionsExtensions
{
    /// <summary>Replaces the site name with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Site name.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteName(this Material3ThemeOptions options, ApiCompatString value) =>
        options with { SiteName = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the site name with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 site-name bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteName(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { SiteName = value };
    }

    /// <summary>Replaces the site name with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 site-name bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteName(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { SiteName = value.ToArray() };

    /// <summary>Replaces the logo href with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 logo href bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLogo(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { Logo = value };
    }

    /// <summary>Replaces the logo href with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 logo href bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLogo(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { Logo = value.ToArray() };

    /// <summary>Replaces the absolute site URL (<c>site_url</c>) with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Absolute site URL (e.g. <c>https://reactiveui.net</c>).</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteUrl(this Material3ThemeOptions options, ApiCompatString value) =>
        options with { SiteUrl = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the absolute site URL with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 site URL bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteUrl(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { SiteUrl = value };
    }

    /// <summary>Replaces the absolute site URL with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 site URL bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteUrl(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { SiteUrl = value.ToArray() };

    /// <summary>Replaces the language code with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Language code (e.g. <c>en</c>).</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLanguage(this Material3ThemeOptions options, ApiCompatString value) =>
        options with { Language = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the language code with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 language-code bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLanguage(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { Language = value };
    }

    /// <summary>Replaces the language code with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 language-code bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLanguage(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { Language = value.ToArray() };

    /// <summary>Replaces the copyright line with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Copyright text.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCopyright(this Material3ThemeOptions options, ApiCompatString value) =>
        options with { Copyright = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the copyright line with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 copyright bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCopyright(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { Copyright = value };
    }

    /// <summary>Replaces the copyright line with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 copyright bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCopyright(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { Copyright = value.ToArray() };

    /// <summary>Replaces the repository URL with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Repository URL.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithRepoUrl(this Material3ThemeOptions options, ApiCompatString value) =>
        options with { RepoUrl = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the repository URL with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 repo URL bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithRepoUrl(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { RepoUrl = value };
    }

    /// <summary>Replaces the repository URL with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 repo URL bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithRepoUrl(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { RepoUrl = value.ToArray() };

    /// <summary>Replaces the edit-URI prefix with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Edit-URI prefix (e.g. <c>edit/main/docs</c>).</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEditUri(this Material3ThemeOptions options, ApiCompatString value) =>
        options with { EditUri = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the edit-URI prefix with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 edit-URI bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEditUri(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { EditUri = value };
    }

    /// <summary>Replaces the edit-URI prefix with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 edit-URI bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEditUri(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { EditUri = value.ToArray() };

    /// <summary>Replaces the embedded-asset root with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">URL prefix for embedded assets (e.g. <c>/assets</c>).</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEmbeddedAssetRoot(this Material3ThemeOptions options, ApiCompatString value) =>
        options with { EmbeddedAssetRoot = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the embedded-asset root with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 asset-root bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEmbeddedAssetRoot(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { EmbeddedAssetRoot = value };
    }

    /// <summary>Replaces the embedded-asset root with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 asset-root bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEmbeddedAssetRoot(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { EmbeddedAssetRoot = value.ToArray() };

    /// <summary>Replaces the CDN root with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">CDN URL prefix.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCdnRoot(this Material3ThemeOptions options, ApiCompatString value) =>
        options with { CdnRoot = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the CDN root with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 CDN-root bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCdnRoot(this Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { CdnRoot = value };
    }

    /// <summary>Replaces the CDN root with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 CDN-root bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCdnRoot(this Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { CdnRoot = value.ToArray() };

    /// <summary>Sets whether prev/next footer links stop at the closest enclosing section.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">True to scope prev/next to the current section; false to traverse the full nav.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSectionScopedFooter(this Material3ThemeOptions options, bool value) =>
        options with { SectionScopedFooter = value };
}
