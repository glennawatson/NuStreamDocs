// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.I18n;

/// <summary>Fluent configuration helpers for <see cref="I18nOptions"/>.</summary>
public static class I18nOptionsExtensions
{
    /// <summary>Extension members for <see cref="I18nOptions"/>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in I18nOptions options)
    {
        /// <summary>Appends or replaces a locale in <see cref="I18nOptions.Locales"/>.</summary>
        /// <param name="code">Non-empty BCP-47 locale code.</param>
        /// <param name="displayName">UTF-8 display name.</param>
        /// <returns>Updated options.</returns>
        public I18nOptions AddLocale(string code, ReadOnlySpan<byte> displayName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            var entry = new LocaleDefinition(code, displayName.ToArray());
            for (var i = 0; i < options.Locales.Length; i++)
            {
                if (!string.Equals(options.Locales[i].Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                LocaleDefinition[] copy = [.. options.Locales];
                copy[i] = entry;
                return options with { Locales = copy };
            }

            return options with { Locales = ArrayJoiner.Concat(options.Locales, [entry]) };
        }

        /// <summary>Sets the primary <see cref="I18nOptions.DefaultLocale"/>.</summary>
        /// <param name="code">Non-empty BCP-47 locale code.</param>
        /// <returns>Updated options.</returns>
        public I18nOptions WithDefaultLocale(string code)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            return options with { DefaultLocale = code };
        }

        /// <summary>Sets a UTF-8 Markdown banner prepended to fallback pages when a translation is missing.</summary>
        /// <param name="markdown">UTF-8 Markdown banner text.</param>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public I18nOptions WithUntranslatedNotice(ReadOnlySpan<byte> markdown) =>
            options with { UntranslatedNotice = markdown.ToArray() };

        /// <summary>Turns off synthesizing fallback pages for missing translations.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public I18nOptions WithoutFallback() =>
            options with { FallbackToDefaultLocale = false };

        /// <summary>Turns off <c>hreflang</c> head link injection.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public I18nOptions WithoutHreflang() =>
            options with { EmitHreflangTags = false };

        /// <summary>Turns off language switcher HTML injection.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public I18nOptions WithoutLanguageSwitcher() =>
            options with { EmitLanguageSwitcher = false };

        /// <summary>Turns off <c>sitemap.xml</c> <c>xhtml:link</c> enrichment.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public I18nOptions WithoutSitemapEnrichment() =>
            options with { EnrichSitemap = false };

        /// <summary>Disables the i18n plugin.</summary>
        /// <returns>Updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public I18nOptions Disable() =>
            options with { Enabled = false };
    }
}
