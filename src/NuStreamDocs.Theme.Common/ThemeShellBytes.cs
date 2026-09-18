// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Text;

namespace NuStreamDocs.Theme.Common;

/// <summary>Shared UTF-8 constants and template-data keys used by the theme shell.</summary>
internal static class ThemeShellBytes
{
    /// <summary>Gets the UTF-8 root-relative site URL.</summary>
    internal static byte[] SiteRoot { get; } = [.. "/"u8];

    /// <summary>Gets the UTF-8 truthy flag emitted for enabled boolean options.</summary>
    internal static byte[] Truthy { get; } = [.. "1"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>language</c>.</summary>
    internal static byte[] LanguageKey { get; } = [.. "language"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>site_name</c>.</summary>
    internal static byte[] SiteNameKey { get; } = [.. "site_name"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>logo</c>.</summary>
    internal static byte[] LogoKey { get; } = [.. "logo"u8];

    /// <summary>Gets the UTF-8 template variable for the absolute site URL.</summary>
    internal static byte[] SiteUrlKey { get; } = [.. "site_url"u8];

    /// <summary>Gets the UTF-8 template variable for the per-page canonical URL.</summary>
    internal static byte[] CanonicalUrlKey { get; } = [.. "canonical_url"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>site_root</c>.</summary>
    internal static byte[] SiteRootKey { get; } = [.. "site_root"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>page_title</c>.</summary>
    internal static byte[] PageTitleKey { get; } = [.. "page_title"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>body</c>.</summary>
    internal static byte[] BodyKey { get; } = [.. "body"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>asset_root</c>.</summary>
    internal static byte[] AssetRootKey { get; } = [.. "asset_root"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>copyright</c>.</summary>
    internal static byte[] CopyrightKey { get; } = [.. "copyright"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>copyright_html</c> (raw, unescaped HTML copyright block).</summary>
    internal static byte[] CopyrightHtmlKey { get; } = [.. "copyright_html"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>footer_partial</c> (raw, unescaped HTML footer-meta replacement loaded from a configured file).</summary>
    internal static byte[] FooterPartialKey { get; } = [.. "footer_partial"u8];

    /// <summary>Gets the UTF-8 template-data key for the <c>social</c> section list.</summary>
    internal static byte[] SocialKey { get; } = [.. "social"u8];

    /// <summary>Gets the UTF-8 template-data key for the truthy <c>social_present</c> flag (set when the social-link list is non-empty).</summary>
    internal static byte[] SocialPresentKey { get; } = [.. "social_present"u8];

    /// <summary>Gets the UTF-8 template-data key for a social link's URL scalar.</summary>
    internal static byte[] SocialUrlKey { get; } = [.. "social_url"u8];

    /// <summary>Gets the UTF-8 template-data key for a social link's title / tooltip scalar.</summary>
    internal static byte[] SocialTitleKey { get; } = [.. "social_title"u8];

    /// <summary>Gets the UTF-8 template-data key for a social link's raw SVG icon scalar.</summary>
    internal static byte[] SocialIconKey { get; } = [.. "social_icon"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>repo_url</c>.</summary>
    internal static byte[] RepoUrlKey { get; } = [.. "repo_url"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>repo_label</c>.</summary>
    internal static byte[] RepoLabelKey { get; } = [.. "repo_label"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>edit_url</c>.</summary>
    internal static byte[] EditUrlKey { get; } = [.. "edit_url"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>scroll_to_top</c>.</summary>
    internal static byte[] ScrollToTopKey { get; } = [.. "scroll_to_top"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>toc_follow</c>.</summary>
    internal static byte[] TocFollowKey { get; } = [.. "toc_follow"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>prev_url</c>.</summary>
    internal static byte[] PrevUrlKey { get; } = [.. "prev_url"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>prev_title</c>.</summary>
    internal static byte[] PrevTitleKey { get; } = [.. "prev_title"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>next_url</c>.</summary>
    internal static byte[] NextUrlKey { get; } = [.. "next_url"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>next_title</c>.</summary>
    internal static byte[] NextTitleKey { get; } = [.. "next_title"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>head_extras</c>.</summary>
    internal static byte[] HeadExtrasKey { get; } = [.. "head_extras"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>description</c>.</summary>
    internal static byte[] DescriptionKey { get; } = [.. "description"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>author</c>.</summary>
    internal static byte[] AuthorKey { get; } = [.. "author"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>hide_navigation</c>.</summary>
    internal static byte[] HideNavigationKey { get; } = [.. "hide_navigation"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>hide_toc</c>.</summary>
    internal static byte[] HideTocKey { get; } = [.. "hide_toc"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>favicon</c>.</summary>
    internal static byte[] FaviconKey { get; } = [.. "favicon"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>generator</c>.</summary>
    internal static byte[] GeneratorKey { get; } = [.. "generator"u8];

    /// <summary>Gets the UTF-8 template-data key for <c>build_date</c>.</summary>
    internal static byte[] BuildDateKey { get; } = [.. "build_date"u8];

    /// <summary>Gets the UTF-8 token consumers can embed in their copyright string; replaced with <see cref="CurrentYear"/> at render time.</summary>
    internal static byte[] YearToken { get; } = [.. "{year}"u8];

    /// <summary>Gets the UTF-8 generator value emitted as <c>nustreamdocs-{version}</c>.</summary>
    internal static byte[] Generator { get; } = BuildGeneratorBytes();

    /// <summary>Gets the UTF-8 ISO 8601 build timestamp.</summary>
    internal static byte[] BuildDate { get; } = Encoding.UTF8.GetBytes(
        TimeProvider.System.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));

    /// <summary>Gets the UTF-8 four-digit current year.</summary>
    internal static byte[] CurrentYear { get; } = Encoding.UTF8.GetBytes(
        TimeProvider.System.GetUtcNow().Year.ToString(CultureInfo.InvariantCulture));

    /// <summary>Builds the <c>nustreamdocs-{version}</c> generator value.</summary>
    /// <returns>UTF-8 bytes of the generator string.</returns>
    private static byte[] BuildGeneratorBytes()
    {
        const string Prefix = "nustreamdocs-";
        var informational = typeof(ThemeShellBytes).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var span = informational.AsSpan();
        var plus = span.IndexOf('+');
        if (plus >= 0)
        {
            span = span[..plus];
        }

        var dst = new byte[(Prefix.Length + span.Length)];
        for (var i = 0; i < Prefix.Length; i++)
        {
            dst[i] = (byte)Prefix[i];
        }

        for (var i = 0; i < span.Length; i++)
        {
            // Semantic-version characters are ASCII; safe to narrow.
            dst[Prefix.Length + i] = (byte)span[i];
        }

        return dst;
    }
}
