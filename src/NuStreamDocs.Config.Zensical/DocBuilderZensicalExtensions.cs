// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Config.MkDocs;

namespace NuStreamDocs.Config.Zensical;

/// <summary>Builder-extension surface for applying a Zensical TOML config to a <see cref="DocBuilder"/>.</summary>
public static class DocBuilderZensicalExtensions
{
    /// <summary>Reads <paramref name="tomlPath"/> as a <c>zensical.toml</c> file and applies its site-level metadata to <paramref name="builder"/>.</summary>
    /// <param name="builder">Target builder.</param>
    /// <param name="tomlPath">Absolute or relative path to a zensical.toml file.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseZensicalConfig(this DocBuilder builder, string tomlPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tomlPath);

        var bytes = File.ReadAllBytes(tomlPath);
        return builder.UseZensicalConfig((ReadOnlySpan<byte>)bytes);
    }

    /// <summary>Parses <paramref name="utf8Toml"/> as a zensical.toml document and applies its site-level metadata to <paramref name="builder"/>.</summary>
    /// <param name="builder">Target builder.</param>
    /// <param name="utf8Toml">UTF-8 TOML bytes.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseZensicalConfig(this DocBuilder builder, ReadOnlySpan<byte> utf8Toml)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var config = ConfigReaderJsonPipeline.Read(utf8Toml, TomlToJson.Convert);
        return builder.ApplyMkDocsConfig(in config);
    }
}
