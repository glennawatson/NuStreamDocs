// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Theme.Material;

/// <summary>Construction helpers for <see cref="MaterialThemeOptions"/>.</summary>
public static class MaterialThemeOptionsExtensions
{
    /// <summary>Extension members for <c>MaterialThemeOptions</c>.</summary>
    /// <param name="options">The options to update.</param>
    extension(in MaterialThemeOptions options)
    {
        /// <summary>Replaces the site name with <paramref name="value"/>.</summary>
        /// <param name="value">Site name.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithSiteName(in ApiCompatString value) =>
            options with { SiteName = Utf8Encoder.Encode(value) };

        /// <summary>Replaces the site name with the supplied UTF-8 bytes.</summary>
        /// <param name="value">UTF-8 site-name bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithSiteName(byte[] value) =>
            options with { SiteName = value };

        /// <summary>Replaces the site name with the supplied UTF-8 span (e.g. a <c>"..."u8</c> literal).</summary>
        /// <param name="value">UTF-8 site-name bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithSiteName(ReadOnlySpan<byte> value) =>
            options with { SiteName = value.ToArray() };

        /// <summary>Replaces the absolute site URL (<c>site_url</c>) with <paramref name="value"/>.</summary>
        /// <param name="value">Absolute site URL (e.g. <c>https://reactiveui.net</c>).</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithSiteUrl(in ApiCompatString value) =>
            options with { SiteUrl = Utf8Encoder.Encode(value) };

        /// <summary>Replaces the absolute site URL with the supplied UTF-8 bytes.</summary>
        /// <param name="value">UTF-8 site URL bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithSiteUrl(byte[] value) =>
            options with { SiteUrl = value };

        /// <summary>Replaces the absolute site URL with the supplied UTF-8 span.</summary>
        /// <param name="value">UTF-8 site URL bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithSiteUrl(ReadOnlySpan<byte> value) =>
            options with { SiteUrl = value.ToArray() };

        /// <summary>Replaces the language code with <paramref name="value"/>.</summary>
        /// <param name="value">Language code (e.g. <c>en</c>).</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithLanguage(in ApiCompatString value) =>
            options with { Language = Utf8Encoder.Encode(value) };

        /// <summary>Replaces the language code with the supplied UTF-8 bytes.</summary>
        /// <param name="value">UTF-8 language-code bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithLanguage(byte[] value) =>
            options with { Language = value };

        /// <summary>Replaces the language code with the supplied UTF-8 span.</summary>
        /// <param name="value">UTF-8 language-code bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithLanguage(ReadOnlySpan<byte> value) =>
            options with { Language = value.ToArray() };

        /// <summary>Replaces the copyright line with <paramref name="value"/>.</summary>
        /// <param name="value">Copyright text.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithCopyright(in ApiCompatString value) =>
            options with { Copyright = Utf8Encoder.Encode(value) };

        /// <summary>Replaces the copyright line with the supplied UTF-8 bytes.</summary>
        /// <param name="value">UTF-8 copyright bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithCopyright(byte[] value) =>
            options with { Copyright = value };

        /// <summary>Replaces the copyright line with the supplied UTF-8 span.</summary>
        /// <param name="value">UTF-8 copyright bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithCopyright(ReadOnlySpan<byte> value) =>
            options with { Copyright = value.ToArray() };

        /// <summary>Replaces the repository URL with <paramref name="value"/>.</summary>
        /// <param name="value">Repository URL.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithRepoUrl(in ApiCompatString value) =>
            options with { RepoUrl = Utf8Encoder.Encode(value) };

        /// <summary>Replaces the repository URL with the supplied UTF-8 bytes.</summary>
        /// <param name="value">UTF-8 repo URL bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithRepoUrl(byte[] value) =>
            options with { RepoUrl = value };

        /// <summary>Replaces the repository URL with the supplied UTF-8 span.</summary>
        /// <param name="value">UTF-8 repo URL bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithRepoUrl(ReadOnlySpan<byte> value) =>
            options with { RepoUrl = value.ToArray() };

        /// <summary>Replaces the edit-URI prefix with <paramref name="value"/>.</summary>
        /// <param name="value">Edit-URI prefix (e.g. <c>edit/main/docs</c>).</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithEditUri(in ApiCompatString value) =>
            options with { EditUri = Utf8Encoder.Encode(value) };

        /// <summary>Replaces the edit-URI prefix with the supplied UTF-8 bytes.</summary>
        /// <param name="value">UTF-8 edit-URI bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithEditUri(byte[] value) =>
            options with { EditUri = value };

        /// <summary>Replaces the edit-URI prefix with the supplied UTF-8 span.</summary>
        /// <param name="value">UTF-8 edit-URI bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithEditUri(ReadOnlySpan<byte> value) =>
            options with { EditUri = value.ToArray() };

        /// <summary>Replaces the embedded-asset root with <paramref name="value"/>.</summary>
        /// <param name="value">URL prefix for embedded assets (e.g. <c>/assets</c>).</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithEmbeddedAssetRoot(
            in ApiCompatString value) =>
            options with { EmbeddedAssetRoot = Utf8Encoder.Encode(value) };

        /// <summary>Replaces the embedded-asset root with the supplied UTF-8 bytes.</summary>
        /// <param name="value">UTF-8 asset-root bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithEmbeddedAssetRoot(byte[] value) =>
            options with { EmbeddedAssetRoot = value };

        /// <summary>Replaces the embedded-asset root with the supplied UTF-8 span.</summary>
        /// <param name="value">UTF-8 asset-root bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithEmbeddedAssetRoot(
            ReadOnlySpan<byte> value) =>
            options with { EmbeddedAssetRoot = value.ToArray() };

        /// <summary>Replaces the CDN root with <paramref name="value"/>.</summary>
        /// <param name="value">CDN URL prefix.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithCdnRoot(in ApiCompatString value) =>
            options with { CdnRoot = Utf8Encoder.Encode(value) };

        /// <summary>Replaces the CDN root with the supplied UTF-8 bytes.</summary>
        /// <param name="value">UTF-8 CDN-root bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithCdnRoot(byte[] value) =>
            options with { CdnRoot = value };

        /// <summary>Replaces the CDN root with the supplied UTF-8 span.</summary>
        /// <param name="value">UTF-8 CDN-root bytes.</param>
        /// <returns>The updated options.</returns>
        public MaterialThemeOptions WithCdnRoot(ReadOnlySpan<byte> value) =>
            options with { CdnRoot = value.ToArray() };
    }
}
