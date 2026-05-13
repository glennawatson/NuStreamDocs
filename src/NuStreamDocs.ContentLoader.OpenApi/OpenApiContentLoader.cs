// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Config.MkDocs;
using NuStreamDocs.ContentLoader.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.OpenApi;

/// <summary>
/// Reads an OpenAPI 3.x specification — JSON or YAML, from a local file or a URL — and turns it into
/// reference pages: one page per tag, listing each operation with its parameters, request body, and
/// responses. <c>$ref</c>-resolved schemas, examples, and security schemes are not expanded.
/// </summary>
public sealed class OpenApiContentLoader : IContentLoader
{
    /// <summary>Local spec file, or empty when reading from a URL.</summary>
    private readonly FilePath _specFile;

    /// <summary>Spec URL, or empty when reading from a local file.</summary>
    private readonly UrlPath _specUrl;

    /// <summary>Local subdirectory the pages are placed under.</summary>
    private readonly PathSegment _routePrefix;

    /// <summary>HTTP client factory; null means the loader owns a short-lived client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="OpenApiContentLoader"/> class reading a local spec file.</summary>
    /// <param name="specFile">Spec file path (absolute, or relative to the input root).</param>
    /// <param name="routePrefix">Local subdirectory the pages are placed under.</param>
    public OpenApiContentLoader(FilePath specFile, PathSegment routePrefix)
        : this(specFile, default, routePrefix, null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OpenApiContentLoader"/> class reading a spec URL.</summary>
    /// <param name="specUrl">Spec URL.</param>
    /// <param name="routePrefix">Local subdirectory the pages are placed under.</param>
    public OpenApiContentLoader(UrlPath specUrl, PathSegment routePrefix)
        : this(default, specUrl, routePrefix, null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OpenApiContentLoader"/> class reading a spec URL with a logger and HTTP factory.</summary>
    /// <param name="specUrl">Spec URL.</param>
    /// <param name="routePrefix">Local subdirectory the pages are placed under.</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client; null means the loader owns a short-lived client.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public OpenApiContentLoader(
        UrlPath specUrl,
        PathSegment routePrefix,
        Func<HttpClient>? httpClientFactory,
        ILogger logger)
        : this(default, specUrl, routePrefix, httpClientFactory, logger)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OpenApiContentLoader"/> class.</summary>
    /// <param name="specFile">Spec file path, or empty when reading from a URL.</param>
    /// <param name="specUrl">Spec URL, or empty when reading from a local file.</param>
    /// <param name="routePrefix">Local subdirectory the pages are placed under.</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client; null means the loader owns a short-lived client.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    private OpenApiContentLoader(
        FilePath specFile,
        UrlPath specUrl,
        PathSegment routePrefix,
        Func<HttpClient>? httpClientFactory,
        ILogger logger)
    {
        if (string.IsNullOrEmpty(specFile.Value) == string.IsNullOrEmpty(specUrl.Value))
        {
            throw new ArgumentException("Exactly one of a spec file path or a spec URL must be supplied.");
        }

        _specFile = specFile;
        _specUrl = specUrl;
        _routePrefix = routePrefix;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "openapi"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        var raw = await ReadSpecAsync(context.InputRoot, cancellationToken).ConfigureAwait(false);
        var json = LooksLikeJson(raw) ? raw : ConvertYamlToJson(raw);
        return OpenApiPageBuilder.Build(json, RoutePrefixBytes(_routePrefix));
    }

    /// <summary>Returns the UTF-8 bytes of the route prefix, or an empty span when it is empty.</summary>
    /// <param name="prefix">Route prefix.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] RoutePrefixBytes(PathSegment prefix) =>
        string.IsNullOrEmpty(prefix.Value) ? [] : Encoding.UTF8.GetBytes(prefix.Value);

    /// <summary>True when <paramref name="bytes"/> begins (after whitespace) with a JSON object or array opener.</summary>
    /// <param name="bytes">Candidate document bytes.</param>
    /// <returns><see langword="true"/> for JSON; <see langword="false"/> for YAML.</returns>
    private static bool LooksLikeJson(byte[] bytes)
    {
        var i = AsciiByteHelpers.SkipWhitespace(bytes, 0);
        return i < bytes.Length && bytes[i] is (byte)'{' or (byte)'[';
    }

    /// <summary>Converts a YAML document to its JSON byte equivalent.</summary>
    /// <param name="yaml">UTF-8 YAML bytes.</param>
    /// <returns>UTF-8 JSON bytes.</returns>
    private static byte[] ConvertYamlToJson(byte[] yaml)
    {
        ArrayBufferWriter<byte> buffer = new(yaml.Length);
        using (Utf8JsonWriter writer = new(buffer))
        {
            YamlToJson.Convert(yaml, writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Reads the spec bytes from the configured file or URL.</summary>
    /// <param name="inputRoot">Build input root (used to resolve a relative file path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw spec bytes.</returns>
    private Task<byte[]> ReadSpecAsync(DirectoryPath inputRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_specFile.Value))
        {
            return FetchAsync(cancellationToken);
        }

        var path = Path.IsPathRooted(_specFile.Value) || inputRoot.IsEmpty
            ? _specFile
            : inputRoot.File(_specFile.Value);
        if (!File.Exists(path.Value))
        {
            throw new ContentLoaderException(StringCompose.Concat("OpenAPI spec file not found: ", path.Value));
        }

        return File.ReadAllBytesAsync(path.Value, cancellationToken);
    }

    /// <summary>Fetches the spec from its URL.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw spec bytes.</returns>
    private async Task<byte[]> FetchAsync(CancellationToken cancellationToken)
    {
        Uri endpoint = new(_specUrl.Value, UriKind.Absolute);
        if (_httpClientFactory is not null)
        {
            return await GetAsync(_httpClientFactory(), endpoint, cancellationToken).ConfigureAwait(false);
        }

        using HttpClient owned = new();
        return await GetAsync(owned, endpoint, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Issues the GET and reads the body.</summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="endpoint">Spec URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw response body.</returns>
    private async Task<byte[]> GetAsync(HttpClient client, Uri endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            ContentLoaderLoggingHelper.LogFetchFailed(_logger, "openapi", _specUrl.Value, ex.Message);
            throw new ContentLoaderException(
                StringCompose.Concat("Failed to fetch OpenAPI spec ", _specUrl.Value, ": ", ex.Message),
                ex);
        }
    }
}
