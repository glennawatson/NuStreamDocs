// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.Schemas;

/// <summary>Loads a JSON Schema (Draft-07 or 2020-12, JSON or YAML) into a Markdown reference page.</summary>
[System.Diagnostics.DebuggerDisplay("JsonSchemaContentLoader: {_schemaFile}")]
public sealed class JsonSchemaContentLoader : IContentLoader
{
    /// <summary>Local schema file path.</summary>
    private readonly FilePath _schemaFile;

    /// <summary>In-memory schema bytes when supplied directly.</summary>
    private readonly byte[]? _inlineBytes;

    /// <summary>Fallback page name stem.</summary>
    private readonly string _fallbackName;

    /// <summary>Destination subdirectory prefix.</summary>
    private readonly PathSegment _routePrefix;

    /// <summary>Initializes a new instance of the <see cref="JsonSchemaContentLoader"/> class from a local file.</summary>
    /// <param name="schemaFile">Path to a <c>.json</c> or <c>.yaml</c> JSON Schema file.</param>
    /// <param name="routePrefix">Subdirectory under which the reference page is emitted.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="schemaFile"/> is empty.</exception>
    public JsonSchemaContentLoader(FilePath schemaFile, PathSegment routePrefix)
    {
        if (schemaFile.IsEmpty)
        {
            throw new ArgumentException("Schema file path must be non-empty.", nameof(schemaFile));
        }

        _schemaFile = schemaFile;
        _fallbackName = schemaFile.FileNameWithoutExtension;
        _routePrefix = routePrefix;
    }

    /// <summary>Initializes a new instance of the <see cref="JsonSchemaContentLoader"/> class from in-memory UTF-8 bytes.</summary>
    /// <param name="schemaBytes">UTF-8 JSON or YAML schema bytes.</param>
    /// <param name="pageName">Page slug or title fallback.</param>
    /// <param name="routePrefix">Subdirectory under which the reference page is emitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="schemaBytes"/> or <paramref name="pageName"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pageName"/> is empty or whitespace.</exception>
    public JsonSchemaContentLoader(byte[] schemaBytes, string pageName, PathSegment routePrefix)
    {
        ArgumentNullException.ThrowIfNull(schemaBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);
        _inlineBytes = schemaBytes;
        _fallbackName = pageName;
        _routePrefix = routePrefix;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "json-schema"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        var raw = _inlineBytes
            ?? await SchemaSourceHelper.ReadLocalFileAsync(_schemaFile, context.InputRoot, "JSON Schema", cancellationToken).ConfigureAwait(false);
        var json = SchemaSourceHelper.NormalizeJsonOrYaml(raw);
        return JsonSchemaPageBuilder.Build(json, _routePrefix, _fallbackName);
    }
}
