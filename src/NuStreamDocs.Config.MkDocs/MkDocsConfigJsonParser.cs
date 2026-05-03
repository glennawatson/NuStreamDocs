// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Format-neutral helper that reads a <see cref="MkDocsConfig"/> from a UTF-8 JSON byte span.
/// </summary>
/// <remarks>
/// Both <c>NuStreamDocs.Config.MkDocs</c> (YAML) and <c>NuStreamDocs.Config.Zensical</c> (TOML)
/// emit JSON with the same shape and route through this parser, so the post-conversion path stays
/// shared and AOT-clean. Nav parsing lives in the dialect-specific reader assemblies and produces
/// <c>NavEntry[]</c> directly into <c>NavOptions.CuratedEntries</c> — this parser handles only
/// site-level metadata.
/// </remarks>
public static class MkDocsConfigJsonParser
{
    /// <summary>Default theme when none is specified.</summary>
    private const string DefaultThemeName = "material";

    /// <summary>Parses a config from a UTF-8 JSON span.</summary>
    /// <param name="utf8Json">UTF-8 JSON bytes.</param>
    /// <returns>The parsed config.</returns>
    public static MkDocsConfig FromJson(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json, isFinalBlock: true, state: default);
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var siteName = root.TryGetProperty("site_name"u8, out var n) ? n.GetString() ?? string.Empty : string.Empty;
        var siteUrl = root.TryGetProperty("site_url"u8, out var u) ? u.GetString() : null;
        var themeName = ReadThemeName(root);
        var useDirectoryUrls = !root.TryGetProperty("use_directory_urls"u8, out var d) || ReadBool(d, defaultValue: true);
        var siteAuthor = root.TryGetProperty("site_author"u8, out var a) ? a.GetString() : null;

        return new(siteName, siteUrl, themeName, useDirectoryUrls, siteAuthor);
    }

    /// <summary>Reads a JSON boolean, falling back to <paramref name="defaultValue"/> for unexpected shapes.</summary>
    /// <param name="element">JSON element.</param>
    /// <param name="defaultValue">Value to return when <paramref name="element"/> is not a boolean.</param>
    /// <returns>The decoded boolean.</returns>
    private static bool ReadBool(in JsonElement element, bool defaultValue) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => defaultValue,
    };

    /// <summary>Reads the <c>theme</c> name from the config root.</summary>
    /// <param name="root">Root JSON element.</param>
    /// <returns>The configured theme name; defaults to <c>material</c>.</returns>
    private static string ReadThemeName(in JsonElement root)
    {
        if (!root.TryGetProperty("theme"u8, out var theme))
        {
            return DefaultThemeName;
        }

        return theme.ValueKind switch
        {
            JsonValueKind.String => theme.GetString() ?? DefaultThemeName,
            JsonValueKind.Object when theme.TryGetProperty("name"u8, out var name) => name.GetString() ?? DefaultThemeName,
            _ => DefaultThemeName,
        };
    }
}
