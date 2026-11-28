// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.I18n;

/// <summary>Maps relative Markdown paths (<c>en/guide.md</c> or <c>guide.fr.md</c>) to <c>(locale, logicalKey)</c> pairs and URLs.</summary>
internal static class LocaleRouteClassifier
{
    /// <summary>Length of the <c>.md</c> extension.</summary>
    private const int MdExtensionLength = 3;

    /// <summary>Length of the <c>./</c> root index marker in directory-URL mode.</summary>
    private const int SelfDirLength = 2;

    /// <summary>Classifies <paramref name="relativePath"/> against the configured locale set.</summary>
    /// <param name="relativePath">Page path relative to input root.</param>
    /// <param name="knownLocales">Configured locale codes (case-insensitive).</param>
    /// <param name="defaultLocale">Fallback locale code when unprefixed and unsuffixed.</param>
    /// <param name="locale">Resolved canonical locale code.</param>
    /// <param name="logicalPath">Locale-neutral forward-slashed path (for example <c>"guide/setup.md"</c>).</param>
    internal static void Classify(
        in FilePath relativePath,
        HashSet<string> knownLocales,
        string defaultLocale,
        out string locale,
        out string logicalPath)
    {
        var normalized = (relativePath.Value ?? string.Empty).Replace('\\', '/').TrimStart('/');
        var firstSlash = normalized.IndexOf('/', StringComparison.Ordinal);
        if (firstSlash > 0)
        {
            var candidatePrefix = normalized[..firstSlash];
            if (TryMatchKnownLocale(candidatePrefix, knownLocales, out var matchedPrefix))
            {
                locale = matchedPrefix;
                logicalPath = normalized[(firstSlash + 1)..];
                return;
            }
        }

        if (TryExtractSuffixLocale(normalized, knownLocales, out var suffixLocale, out var strippedPath))
        {
            locale = suffixLocale;
            logicalPath = strippedPath;
            return;
        }

        locale = defaultLocale;
        logicalPath = normalized;
    }

    /// <summary>Composes the directory-prefixed relative Markdown path for <paramref name="locale"/> and <paramref name="logicalPath"/>.</summary>
    /// <param name="locale">Target locale code.</param>
    /// <param name="logicalPath">Locale-neutral path.</param>
    /// <returns>Forward-slashed relative path (<c>"{locale}/{logicalPath}"</c>).</returns>
    internal static FilePath ComposePrefixedPath(string locale, string logicalPath) =>
        new($"{locale}/{logicalPath}");

    /// <summary>Composes a public href (<c>https://site/en/guide/</c> or <c>/en/guide/</c>) for <paramref name="relativePath"/>.</summary>
    /// <param name="relativePath">Page relative path.</param>
    /// <param name="baseUrl">Normalized trailing-slash site URL bytes (or empty for root-relative hrefs).</param>
    /// <param name="useDirectoryUrls">Directory-URL flag.</param>
    /// <returns>UTF-8 href bytes.</returns>
    internal static byte[] ComposeHref(
        in FilePath relativePath,
        ReadOnlySpan<byte> baseUrl,
        bool useDirectoryUrls)
    {
        var pageUrl = Utf8MarkdownUrl.FromRelativePath(relativePath, useDirectoryUrls).AsSpan();
        if (pageUrl.SequenceEqual("./"u8))
        {
            pageUrl = pageUrl[SelfDirLength..];
        }

        return !baseUrl.IsEmpty
            ? [.. baseUrl, .. pageUrl]
            : [.. "/"u8, .. pageUrl];
    }

    /// <summary>Checks whether <paramref name="normalized"/> ends with <c>.{locale}.md</c> and strips the locale segment.</summary>
    /// <param name="normalized">Forward-slashed path.</param>
    /// <param name="knownLocales">Configured locale codes.</param>
    /// <param name="locale">Matched locale code.</param>
    /// <param name="strippedPath">Path with <c>.{locale}</c> removed.</param>
    /// <returns><c>true</c> when a known suffix locale matched.</returns>
    private static bool TryExtractSuffixLocale(
        string normalized,
        HashSet<string> knownLocales,
        out string locale,
        out string strippedPath)
    {
        locale = string.Empty;
        strippedPath = string.Empty;
        if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var withoutMd = normalized[..^MdExtensionLength];
        var lastSlash = withoutMd.LastIndexOf('/');
        var fileStem = lastSlash < 0 ? withoutMd : withoutMd[(lastSlash + 1)..];
        var lastDot = fileStem.LastIndexOf('.');
        if (lastDot <= 0)
        {
            return false;
        }

        var candidateSuffix = fileStem[(lastDot + 1)..];
        if (!TryMatchKnownLocale(candidateSuffix, knownLocales, out locale))
        {
            return false;
        }

        var baseStem = fileStem[..lastDot];
        strippedPath = lastSlash < 0
            ? $"{baseStem}.md"
            : $"{withoutMd[..(lastSlash + 1)]}{baseStem}.md";
        return true;
    }

    /// <summary>Matches <paramref name="candidate"/> case-insensitively against <paramref name="knownLocales"/>, returning the canonical casing stored in the set.</summary>
    /// <param name="candidate">Candidate segment.</param>
    /// <param name="knownLocales">Case-insensitive set of configured locale codes.</param>
    /// <param name="canonical">Canonical locale code.</param>
    /// <returns><c>true</c> when present.</returns>
    private static bool TryMatchKnownLocale(string candidate, HashSet<string> knownLocales, out string canonical)
    {
        if (knownLocales.TryGetValue(candidate, out var found))
        {
            canonical = found;
            return true;
        }

        canonical = string.Empty;
        return false;
    }
}
