// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.I18n;

/// <summary>Configuration for <see cref="I18nPlugin"/>.</summary>
[System.Diagnostics.DebuggerDisplay("I18nOptions: Default={DefaultLocale}, Enabled={Enabled}")]
public readonly record struct I18nOptions()
{
    /// <summary>Gets the default option set configured for English (<c>"en"</c>).</summary>
    public static I18nOptions Default { get; } = new();

    /// <summary>Gets a value indicating whether multi-locale processing is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the primary BCP-47 locale code used for <c>x-default</c> and fallback pages.</summary>
    public string DefaultLocale { get; init; } = "en";

    /// <summary>Gets the ordered list of configured locales.</summary>
    public LocaleDefinition[] Locales { get; init; } = [new("en", [.. DefaultEnglishLabel])];

    /// <summary>Gets a value indicating whether missing translations in non-default locale folders are synthesized from the default locale.</summary>
    public bool FallbackToDefaultLocale { get; init; } = true;

    /// <summary>Gets optional UTF-8 Markdown prepended above fallback pages when a translation is missing.</summary>
    public byte[] UntranslatedNotice { get; init; } = [];

    /// <summary>Gets a value indicating whether <c>&lt;link rel="alternate" hreflang="..."&gt;</c> tags are injected into <c>&lt;head&gt;</c>.</summary>
    public bool EmitHreflangTags { get; init; } = true;

    /// <summary>Gets a value indicating whether an accessible language switcher bar is injected when two or more locales exist.</summary>
    public bool EmitLanguageSwitcher { get; init; } = true;

    /// <summary>Gets a value indicating whether <c>sitemap.xml</c> is enriched with <c>xhtml:link</c> alternate entries at finalize time.</summary>
    public bool EnrichSitemap { get; init; } = true;

    /// <summary>Gets the default English display label bytes.</summary>
    private static ReadOnlySpan<byte> DefaultEnglishLabel => "English"u8;
}
