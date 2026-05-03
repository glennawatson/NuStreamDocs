// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Builder-extension surface for applying mkdocs.yml config to a <see cref="DocBuilder"/>.
/// </summary>
public static class DocBuilderMkDocsExtensions
{
    /// <summary>Reads <paramref name="yamlPath"/> as an <c>mkdocs.yml</c> file and applies its site-level metadata to <paramref name="builder"/>.</summary>
    /// <param name="builder">Target builder.</param>
    /// <param name="yamlPath">Absolute or relative path to an mkdocs.yml file.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMkDocsConfig(this DocBuilder builder, string yamlPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPath);

        var bytes = File.ReadAllBytes(yamlPath);
        return builder.UseMkDocsConfig((ReadOnlySpan<byte>)bytes);
    }

    /// <summary>Parses <paramref name="utf8Yaml"/> as an mkdocs.yml document and applies its site-level metadata to <paramref name="builder"/>.</summary>
    /// <param name="builder">Target builder.</param>
    /// <param name="utf8Yaml">UTF-8 YAML bytes.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMkDocsConfig(this DocBuilder builder, ReadOnlySpan<byte> utf8Yaml)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var config = ConfigReaderJsonPipeline.Read(utf8Yaml, YamlToJson.Convert);
        return builder.ApplyMkDocsConfig(in config);
    }

    /// <summary>Applies a pre-parsed <see cref="MkDocsConfig"/> onto <paramref name="builder"/>.</summary>
    /// <param name="builder">Target builder.</param>
    /// <param name="config">Parsed mkdocs config.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder ApplyMkDocsConfig(this DocBuilder builder, in MkDocsConfig config)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .WithSiteName(config.SiteName)
            .WithSiteUrl(config.SiteUrl)
            .WithSiteAuthor(config.SiteAuthor)
            .UseDirectoryUrls(config.UseDirectoryUrls);
    }
}
