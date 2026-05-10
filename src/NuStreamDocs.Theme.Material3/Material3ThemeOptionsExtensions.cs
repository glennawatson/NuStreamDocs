// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3;

/// <summary>Construction helpers for <see cref="Material3ThemeOptions"/>.</summary>
public static class Material3ThemeOptionsExtensions
{
    /// <summary>Replaces the site name with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Site name.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteName(this in Material3ThemeOptions options, in ApiCompatString value) =>
        options with { SiteName = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the site name with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 site-name bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteName(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { SiteName = value };
    }

    /// <summary>Replaces the site name with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 site-name bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteName(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { SiteName = value.ToArray() };

    /// <summary>Replaces the logo href with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 logo href bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLogo(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { Logo = value };
    }

    /// <summary>Replaces the logo href with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 logo href bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLogo(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { Logo = value.ToArray() };

    /// <summary>Replaces the favicon href with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 favicon href bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithFavicon(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { Favicon = value };
    }

    /// <summary>Replaces the favicon href with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 favicon href bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithFavicon(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { Favicon = value.ToArray() };

    /// <summary>Replaces the absolute site URL (<c>site_url</c>) with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Absolute site URL (e.g. <c>https://reactiveui.net</c>).</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteUrl(this in Material3ThemeOptions options, in ApiCompatString value) =>
        options with { SiteUrl = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the absolute site URL with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 site URL bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteUrl(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { SiteUrl = value };
    }

    /// <summary>Replaces the absolute site URL with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 site URL bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSiteUrl(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { SiteUrl = value.ToArray() };

    /// <summary>Replaces the language code with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Language code (e.g. <c>en</c>).</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLanguage(this in Material3ThemeOptions options, in ApiCompatString value) =>
        options with { Language = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the language code with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 language-code bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLanguage(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { Language = value };
    }

    /// <summary>Replaces the language code with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 language-code bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithLanguage(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { Language = value.ToArray() };

    /// <summary>Replaces the copyright line with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Copyright text.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCopyright(this in Material3ThemeOptions options, in ApiCompatString value) =>
        options with { Copyright = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the copyright line with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 copyright bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCopyright(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { Copyright = value };
    }

    /// <summary>Replaces the copyright line with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 copyright bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCopyright(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { Copyright = value.ToArray() };

    /// <summary>Replaces the raw-HTML copyright block with the supplied UTF-8 bytes; the bytes are emitted verbatim (no escaping) and override <see cref="Material3ThemeOptions.Copyright"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 raw-HTML copyright block.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCopyrightHtml(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { CopyrightHtml = value };
    }

    /// <summary>Replaces the raw-HTML copyright block with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 raw-HTML copyright block.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCopyrightHtml(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { CopyrightHtml = [.. value] };

    /// <summary>Points the footer at an HTML partial whose contents replace the entire footer-meta inner block.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="path">UTF-8 path bytes — relative to the project root (e.g. <c>overrides/footer.html</c>) or absolute.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithFooterPartial(this in Material3ThemeOptions options, byte[] path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return options with { FooterPartialPath = path };
    }

    /// <summary>Points the footer at an HTML partial via a UTF-8 span (e.g. <c>"overrides/footer.html"u8</c>).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="path">UTF-8 path bytes — relative to the project root or absolute.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithFooterPartial(this in Material3ThemeOptions options, ReadOnlySpan<byte> path) =>
        options with { FooterPartialPath = [.. path] };

    /// <summary>Appends a social link to the footer's <c>md-social</c> list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="url">UTF-8 destination URL bytes.</param>
    /// <param name="title">UTF-8 link title / tooltip bytes.</param>
    /// <param name="iconSvg">UTF-8 raw SVG markup bytes emitted verbatim inside the anchor.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions AddSocialLink(this in Material3ThemeOptions options, byte[] url, byte[] title, byte[] iconSvg)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(iconSvg);
        var existing = options.SocialLinks;
        ThemeSocialLink[] next = [.. existing, new(url, title, iconSvg)];
        return options with { SocialLinks = next };
    }

    /// <summary>Replaces the repository URL with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Repository URL.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithRepoUrl(this in Material3ThemeOptions options, in ApiCompatString value) =>
        options with { RepoUrl = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the repository URL with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 repo URL bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithRepoUrl(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { RepoUrl = value };
    }

    /// <summary>Replaces the repository URL with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 repo URL bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithRepoUrl(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { RepoUrl = value.ToArray() };

    /// <summary>Replaces the edit-URI prefix with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">Edit-URI prefix (e.g. <c>edit/main/docs</c>).</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEditUri(this in Material3ThemeOptions options, in ApiCompatString value) =>
        options with { EditUri = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the edit-URI prefix with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 edit-URI bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEditUri(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { EditUri = value };
    }

    /// <summary>Replaces the edit-URI prefix with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 edit-URI bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEditUri(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { EditUri = value.ToArray() };

    /// <summary>Replaces the embedded-asset root with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">URL prefix for embedded assets (e.g. <c>/assets</c>).</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEmbeddedAssetRoot(this in Material3ThemeOptions options, in ApiCompatString value) =>
        options with { EmbeddedAssetRoot = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the embedded-asset root with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 asset-root bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEmbeddedAssetRoot(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { EmbeddedAssetRoot = value };
    }

    /// <summary>Replaces the embedded-asset root with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 asset-root bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithEmbeddedAssetRoot(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { EmbeddedAssetRoot = value.ToArray() };

    /// <summary>Replaces the CDN root with <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">CDN URL prefix.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCdnRoot(this in Material3ThemeOptions options, in ApiCompatString value) =>
        options with { CdnRoot = Utf8Encoder.Encode(value) };

    /// <summary>Replaces the CDN root with the supplied UTF-8 bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 CDN-root bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCdnRoot(this in Material3ThemeOptions options, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return options with { CdnRoot = value };
    }

    /// <summary>Replaces the CDN root with the supplied UTF-8 span.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">UTF-8 CDN-root bytes.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithCdnRoot(this in Material3ThemeOptions options, ReadOnlySpan<byte> value) =>
        options with { CdnRoot = value.ToArray() };

    /// <summary>Sets whether prev/next footer links stop at the closest enclosing section.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="value">True to scope prev/next to the current section; false to traverse the full nav.</param>
    /// <returns>The updated options.</returns>
    public static Material3ThemeOptions WithSectionScopedFooter(this in Material3ThemeOptions options, bool value) =>
        options with { SectionScopedFooter = value };
}
