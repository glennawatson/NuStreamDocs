// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.Schemas;

/// <summary>Loads an AsyncAPI 2.x or 3.x specification (JSON or YAML) into a Markdown reference page.</summary>
[System.Diagnostics.DebuggerDisplay("AsyncApiContentLoader: {_specFile}")]
public sealed class AsyncApiContentLoader : IContentLoader
{
    /// <summary>Local AsyncAPI file path.</summary>
    private readonly FilePath _specFile;

    /// <summary>Optional in-memory AsyncAPI bytes.</summary>
    private readonly byte[]? _inlineBytes;

    /// <summary>Fallback title when <c>info.title</c> is absent.</summary>
    private readonly string _fallbackName;

    /// <summary>Destination subdirectory prefix.</summary>
    private readonly PathSegment _routePrefix;

    /// <summary>Initializes a new instance of the <see cref="AsyncApiContentLoader"/> class from a local file.</summary>
    /// <param name="specFile">Path to a JSON or YAML AsyncAPI file.</param>
    /// <param name="routePrefix">Subdirectory under which the reference page is emitted.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="specFile"/> is empty.</exception>
    public AsyncApiContentLoader(FilePath specFile, PathSegment routePrefix)
    {
        if (specFile.IsEmpty)
        {
            throw new ArgumentException("AsyncAPI spec file path must be non-empty.", nameof(specFile));
        }

        _specFile = specFile;
        _fallbackName = specFile.FileNameWithoutExtension;
        _routePrefix = routePrefix;
    }

    /// <summary>Initializes a new instance of the <see cref="AsyncApiContentLoader"/> class from in-memory UTF-8 bytes.</summary>
    /// <param name="specBytes">UTF-8 JSON or YAML AsyncAPI bytes.</param>
    /// <param name="fallbackName">Fallback title when <c>info.title</c> is absent.</param>
    /// <param name="routePrefix">Subdirectory under which the reference page is emitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="specBytes"/> or <paramref name="fallbackName"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fallbackName"/> is empty or whitespace.</exception>
    public AsyncApiContentLoader(byte[] specBytes, string fallbackName, PathSegment routePrefix)
    {
        ArgumentNullException.ThrowIfNull(specBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackName);
        _inlineBytes = specBytes;
        _fallbackName = fallbackName;
        _routePrefix = routePrefix;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "asyncapi"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        var raw = _inlineBytes
            ?? await SchemaSourceHelper.ReadLocalFileAsync(_specFile, context.InputRoot, "AsyncAPI", cancellationToken).ConfigureAwait(false);
        var json = SchemaSourceHelper.NormalizeJsonOrYaml(raw);
        return AsyncApiPageBuilder.Build(json, _routePrefix, _fallbackName);
    }
}
