// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Non-generic carrier for the byte[] template-data keys and other UTF-8 constants that <see cref="ThemePluginBase{TTheme, TOptions}"/> shares across every closed generic instantiation.
/// </summary>
/// <remarks>
/// Hoisted out of the generic so the same byte arrays back every theme rather than being duplicated per closed type.
/// </remarks>
internal static class ThemeShellBytes
{
    /// <summary>UTF-8 root-relative site URL.</summary>
    public static readonly byte[] SiteRoot = [.. "/"u8];

    /// <summary>UTF-8 truthy flag emitted for enabled boolean options.</summary>
    public static readonly byte[] Truthy = [.. "1"u8];

    /// <summary>UTF-8 template-data key for <c>language</c>.</summary>
    public static readonly byte[] LanguageKey = [.. "language"u8];

    /// <summary>UTF-8 template-data key for <c>site_name</c>.</summary>
    public static readonly byte[] SiteNameKey = [.. "site_name"u8];

    /// <summary>UTF-8 template-data key for <c>logo</c>.</summary>
    public static readonly byte[] LogoKey = [.. "logo"u8];

    /// <summary>UTF-8 template variable for the absolute site URL.</summary>
    public static readonly byte[] SiteUrlKey = [.. "site_url"u8];

    /// <summary>UTF-8 template variable for the per-page canonical URL.</summary>
    public static readonly byte[] CanonicalUrlKey = [.. "canonical_url"u8];

    /// <summary>UTF-8 template-data key for <c>site_root</c>.</summary>
    public static readonly byte[] SiteRootKey = [.. "site_root"u8];

    /// <summary>UTF-8 template-data key for <c>page_title</c>.</summary>
    public static readonly byte[] PageTitleKey = [.. "page_title"u8];

    /// <summary>UTF-8 template-data key for <c>body</c>.</summary>
    public static readonly byte[] BodyKey = [.. "body"u8];

    /// <summary>UTF-8 template-data key for <c>asset_root</c>.</summary>
    public static readonly byte[] AssetRootKey = [.. "asset_root"u8];

    /// <summary>UTF-8 template-data key for <c>copyright</c>.</summary>
    public static readonly byte[] CopyrightKey = [.. "copyright"u8];

    /// <summary>UTF-8 template-data key for <c>repo_url</c>.</summary>
    public static readonly byte[] RepoUrlKey = [.. "repo_url"u8];

    /// <summary>UTF-8 template-data key for <c>repo_label</c>.</summary>
    public static readonly byte[] RepoLabelKey = [.. "repo_label"u8];

    /// <summary>UTF-8 template-data key for <c>edit_url</c>.</summary>
    public static readonly byte[] EditUrlKey = [.. "edit_url"u8];

    /// <summary>UTF-8 template-data key for <c>scroll_to_top</c>.</summary>
    public static readonly byte[] ScrollToTopKey = [.. "scroll_to_top"u8];

    /// <summary>UTF-8 template-data key for <c>toc_follow</c>.</summary>
    public static readonly byte[] TocFollowKey = [.. "toc_follow"u8];

    /// <summary>UTF-8 template-data key for <c>prev_url</c>.</summary>
    public static readonly byte[] PrevUrlKey = [.. "prev_url"u8];

    /// <summary>UTF-8 template-data key for <c>prev_title</c>.</summary>
    public static readonly byte[] PrevTitleKey = [.. "prev_title"u8];

    /// <summary>UTF-8 template-data key for <c>next_url</c>.</summary>
    public static readonly byte[] NextUrlKey = [.. "next_url"u8];

    /// <summary>UTF-8 template-data key for <c>next_title</c>.</summary>
    public static readonly byte[] NextTitleKey = [.. "next_title"u8];

    /// <summary>UTF-8 template-data key for <c>head_extras</c>.</summary>
    public static readonly byte[] HeadExtrasKey = [.. "head_extras"u8];

    /// <summary>UTF-8 template-data key for <c>description</c>.</summary>
    public static readonly byte[] DescriptionKey = [.. "description"u8];

    /// <summary>UTF-8 template-data key for <c>author</c>.</summary>
    public static readonly byte[] AuthorKey = [.. "author"u8];

    /// <summary>UTF-8 template-data key for <c>hide_navigation</c>.</summary>
    public static readonly byte[] HideNavigationKey = [.. "hide_navigation"u8];

    /// <summary>UTF-8 template-data key for <c>hide_toc</c>.</summary>
    public static readonly byte[] HideTocKey = [.. "hide_toc"u8];

    /// <summary>UTF-8 template-data key for <c>favicon</c>.</summary>
    public static readonly byte[] FaviconKey = [.. "favicon"u8];

    /// <summary>UTF-8 template-data key for <c>generator</c>.</summary>
    public static readonly byte[] GeneratorKey = [.. "generator"u8];

    /// <summary>UTF-8 template-data key for <c>build_date</c>.</summary>
    public static readonly byte[] BuildDateKey = [.. "build_date"u8];

    /// <summary>UTF-8 token consumers can embed in their copyright string; replaced with <see cref="CurrentYear"/> at render time.</summary>
    public static readonly byte[] YearToken = [.. "{year}"u8];

    /// <summary>UTF-8 generator value emitted as <c>nustreamdocs-{version}</c>; encoded once at type init.</summary>
    public static readonly byte[] Generator = BuildGeneratorBytes();

    /// <summary>UTF-8 ISO 8601 build timestamp; captured once when this assembly first loads.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarAnalyzer", "S6354", Justification = "Build timestamp is intentionally wall-clock; not unit-tested for content.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarAnalyzer", "S6585", Justification = "ISO 8601 round-trip format is the wire format consumers expect.")]
    public static readonly byte[] BuildDate = System.Text.Encoding.UTF8.GetBytes(
        DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>UTF-8 four-digit current year.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarAnalyzer", "S6354", Justification = "Wall-clock by design — copyright stamping uses UTC year.")]
    public static readonly byte[] CurrentYear = System.Text.Encoding.UTF8.GetBytes(
        DateTimeOffset.UtcNow.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Builds the <c>nustreamdocs-{version}</c> generator value; the version is read from the assembly informational version with any <c>+sha</c> build-metadata suffix stripped.</summary>
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

        var totalChars = Prefix.Length + span.Length;
        var dst = new byte[totalChars];
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
