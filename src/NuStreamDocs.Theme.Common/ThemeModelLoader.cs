// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Templating;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Shared helpers for loading the stock theme model shape.
/// </summary>
public static class ThemeModelLoader
{
    /// <summary>Standard partials used by both built-in Material-derived themes.</summary>
    private static readonly string[] StandardPartialNames =
    [
        "head_styles",
        "body_scripts",
        "header",
        "sidebar",
        "footer",
    ];

    /// <summary>Compiles the shared top-level page template.</summary>
    /// <param name="readBytes">Embedded-asset byte reader.</param>
    /// <returns>The compiled template.</returns>
    public static Template LoadPage(Func<string, byte[]> readBytes)
    {
        ArgumentNullException.ThrowIfNull(readBytes);
        return Template.Compile(readBytes("page.mustache"));
    }

    /// <summary>Compiles the standard <c>partials/*.mustache</c> set into a byte-keyed registry.</summary>
    /// <param name="readBytes">Embedded-asset byte reader.</param>
    /// <returns>A UTF-8 byte-keyed registry of compiled partials.</returns>
    public static Dictionary<byte[], Template> LoadStandardPartials(Func<string, byte[]> readBytes)
    {
        ArgumentNullException.ThrowIfNull(readBytes);

        var working = new Dictionary<byte[], Template>(StandardPartialNames.Length, ByteArrayComparer.Instance);
        for (var i = 0; i < StandardPartialNames.Length; i++)
        {
            var name = StandardPartialNames[i];
            working[Encoding.UTF8.GetBytes(name)] = Template.Compile(readBytes("partials/" + name + ".mustache"));
        }

        return working;
    }

    /// <summary>Loads every static asset listed in <paramref name="staticAssetPaths"/>.</summary>
    /// <param name="staticAssetPaths">Relative asset paths.</param>
    /// <param name="readBytes">Embedded-asset byte reader.</param>
    /// <returns>A plain dictionary keyed by relative output path.</returns>
    public static Dictionary<string, byte[]> LoadStaticAssets(string[] staticAssetPaths, Func<string, byte[]> readBytes)
    {
        ArgumentNullException.ThrowIfNull(staticAssetPaths);
        ArgumentNullException.ThrowIfNull(readBytes);

        var working = new Dictionary<string, byte[]>(staticAssetPaths.Length, StringComparer.Ordinal);
        for (var i = 0; i < staticAssetPaths.Length; i++)
        {
            var path = staticAssetPaths[i];
            working[path] = readBytes(path);
        }

        return working;
    }

    /// <summary>Builds an indexable static-asset snapshot from <paramref name="staticAssets"/>.</summary>
    /// <param name="staticAssets">Plain lookup keyed by relative path.</param>
    /// <returns>Named tuple array for write-out loops.</returns>
    public static (string RelativePath, byte[] Bytes)[] BuildStaticAssetEntries(Dictionary<string, byte[]> staticAssets)
    {
        ArgumentNullException.ThrowIfNull(staticAssets);

        var entries = new (string RelativePath, byte[] Bytes)[staticAssets.Count];
        var index = 0;
        foreach (var entry in staticAssets)
        {
            entries[index] = (entry.Key, entry.Value);
            index++;
        }

        return entries;
    }
}
