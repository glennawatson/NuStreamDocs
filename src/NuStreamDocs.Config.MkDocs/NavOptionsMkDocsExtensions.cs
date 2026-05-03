// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Nav;

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Fluent extensions that load a curated nav tree from an <c>mkdocs.yml</c> (or YAML byte stream)
/// into <see cref="NavOptions.CuratedEntries"/>. Keeps mkdocs-specific serialization out of
/// <c>NuStreamDocs.Nav</c> so the core nav module never references a config dialect.
/// </summary>
public static class NavOptionsMkDocsExtensions
{
    /// <summary>Reads <paramref name="yamlPath"/>, parses its <c>nav:</c> tree, and returns options with the curated list populated.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="yamlPath">Absolute or relative path to an mkdocs.yml file.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions FromMkDocsYaml(this NavOptions options, string yamlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPath);
        var bytes = File.ReadAllBytes(yamlPath);
        return options.FromMkDocsYaml((ReadOnlySpan<byte>)bytes);
    }

    /// <summary>Parses <paramref name="utf8Yaml"/> as an mkdocs.yml document and returns options with the curated list populated.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="utf8Yaml">UTF-8 YAML bytes.</param>
    /// <returns>The updated options.</returns>
    public static NavOptions FromMkDocsYaml(this NavOptions options, ReadOnlySpan<byte> utf8Yaml) =>
        options.WithCuratedEntries(MkDocsNavParser.FromYaml(utf8Yaml));
}
