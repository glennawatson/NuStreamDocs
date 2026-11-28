// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.Schemas;

/// <summary>Loads a GraphQL SDL (<c>.graphql</c> / <c>.gql</c>) schema into a Markdown reference page.</summary>
[System.Diagnostics.DebuggerDisplay("GraphQlSchemaContentLoader: {_schemaFile}")]
public sealed class GraphQlSchemaContentLoader : IContentLoader
{
    /// <summary>Local SDL file path.</summary>
    private readonly FilePath _schemaFile;

    /// <summary>Optional in-memory SDL bytes.</summary>
    private readonly byte[]? _inlineBytes;

    /// <summary>Page title.</summary>
    private readonly string _title;

    /// <summary>Destination subdirectory prefix.</summary>
    private readonly PathSegment _routePrefix;

    /// <summary>Initializes a new instance of the <see cref="GraphQlSchemaContentLoader"/> class from a local SDL file.</summary>
    /// <param name="schemaFile">Path to a <c>.graphql</c> or <c>.gql</c> file.</param>
    /// <param name="routePrefix">Subdirectory under which the reference page is emitted.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="schemaFile"/> is empty.</exception>
    public GraphQlSchemaContentLoader(FilePath schemaFile, PathSegment routePrefix)
        : this(schemaFile, routePrefix, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GraphQlSchemaContentLoader"/> class from a local SDL file with an explicit title.</summary>
    /// <param name="schemaFile">Path to a <c>.graphql</c> or <c>.gql</c> file.</param>
    /// <param name="routePrefix">Subdirectory under which the reference page is emitted.</param>
    /// <param name="title">Optional page title (defaults to the filename stem when <c>null</c> or whitespace).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="schemaFile"/> is empty.</exception>
    public GraphQlSchemaContentLoader(FilePath schemaFile, PathSegment routePrefix, string? title)
    {
        if (schemaFile.IsEmpty)
        {
            throw new ArgumentException("GraphQL schema file path must be non-empty.", nameof(schemaFile));
        }

        _schemaFile = schemaFile;
        _title = string.IsNullOrWhiteSpace(title) ? schemaFile.FileNameWithoutExtension : title;
        _routePrefix = routePrefix;
    }

    /// <summary>Initializes a new instance of the <see cref="GraphQlSchemaContentLoader"/> class from in-memory UTF-8 SDL bytes.</summary>
    /// <param name="sdlBytes">UTF-8 SDL bytes.</param>
    /// <param name="title">Page title and slug source.</param>
    /// <param name="routePrefix">Subdirectory under which the reference page is emitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sdlBytes"/> or <paramref name="title"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is empty or whitespace.</exception>
    public GraphQlSchemaContentLoader(byte[] sdlBytes, string title, PathSegment routePrefix)
    {
        ArgumentNullException.ThrowIfNull(sdlBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _inlineBytes = sdlBytes;
        _title = title;
        _routePrefix = routePrefix;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "graphql"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        var raw = _inlineBytes
            ?? await SchemaSourceHelper.ReadLocalFileAsync(_schemaFile, context.InputRoot, "GraphQL SDL", cancellationToken).ConfigureAwait(false);
        return GraphQlSdlPageBuilder.Build(raw, _title, _routePrefix);
    }
}
