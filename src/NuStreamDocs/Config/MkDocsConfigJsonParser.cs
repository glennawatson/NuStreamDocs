// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;

namespace NuStreamDocs.Config;

/// <summary>
/// Format-neutral helper that reads a <see cref="MkDocsConfig"/> from
/// a UTF-8 JSON byte span.
/// </summary>
/// <remarks>
/// Both <c>NuStreamDocs.Config.MkDocs</c> (YAML) and
/// <c>NuStreamDocs.Config.Zensical</c> (TOML) emit JSON with the same
/// shape and route through this parser, so the post-conversion path
/// stays shared and AOT-clean.
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
        var nav = ReadNav(root);
        var useDirectoryUrls = !root.TryGetProperty("use_directory_urls"u8, out var d) || ReadBool(d, defaultValue: true);

        return new(siteName, siteUrl, themeName, nav, useDirectoryUrls);
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

    /// <summary>Reads the flat-list form of the <c>nav</c> array.</summary>
    /// <param name="root">Root JSON element.</param>
    /// <returns>Parsed nav entries; empty when the array is missing or nested.</returns>
    private static NavEntry[] ReadNav(in JsonElement root)
    {
        if (!root.TryGetProperty("nav"u8, out var nav) || nav.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var capacity = nav.GetArrayLength();
        if (capacity == 0)
        {
            return [];
        }

        var buffer = ArrayPool<NavEntry>.Shared.Rent(capacity);
        try
        {
            var count = 0;
            var items = nav.EnumerateArray();
            while (items.MoveNext())
            {
                var item = items.Current;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var props = item.EnumerateObject();
                while (props.MoveNext())
                {
                    var prop = props.Current;
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        buffer[count++] = new(prop.Name, prop.Value.GetString() ?? string.Empty);
                    }
                }
            }

            return NavBuilder.ToArray(buffer, count);
        }
        finally
        {
            ArrayPool<NavEntry>.Shared.Return(buffer, clearArray: true);
        }
    }
}
