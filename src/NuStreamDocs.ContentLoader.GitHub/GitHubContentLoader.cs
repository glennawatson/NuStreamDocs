// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.ContentLoader.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.GitHub;

/// <summary>
/// Pulls every Markdown file under a path in a GitHub repository (at any branch, tag, or commit) and
/// mounts them under a local route prefix — for example, conceptual docs that live in <c>docs/</c> in
/// the product repo. Uses the GitHub REST API; supply a token for private repos or higher rate limits.
/// </summary>
public sealed class GitHubContentLoader : IContentLoader
{
    /// <summary>The repository point to read from.</summary>
    private readonly GitHubRepoRef _repo;

    /// <summary>Repository subdirectory to include (empty = whole repo).</summary>
    private readonly PathSegment _sourcePath;

    /// <summary>Local subdirectory the files are mounted under (empty = repo-relative).</summary>
    private readonly PathSegment _routePrefix;

    /// <summary>Personal access token; empty for unauthenticated requests.</summary>
    private readonly byte[] _token;

    /// <summary>HTTP client factory; null means the loader owns a short-lived client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="GitHubContentLoader"/> class for an unauthenticated public-repo pull.</summary>
    /// <param name="repo">The repository point to read from.</param>
    /// <param name="sourcePath">Repository subdirectory to include.</param>
    /// <param name="routePrefix">Local subdirectory the files are mounted under.</param>
    public GitHubContentLoader(GitHubRepoRef repo, PathSegment sourcePath, PathSegment routePrefix)
        : this(repo, sourcePath, routePrefix, [], httpClientFactory: null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GitHubContentLoader"/> class.</summary>
    /// <param name="repo">The repository point to read from.</param>
    /// <param name="sourcePath">Repository subdirectory to include.</param>
    /// <param name="routePrefix">Local subdirectory the files are mounted under.</param>
    /// <param name="token">Personal access token; empty for unauthenticated requests.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public GitHubContentLoader(GitHubRepoRef repo, PathSegment sourcePath, PathSegment routePrefix, byte[] token, ILogger logger)
        : this(repo, sourcePath, routePrefix, token, httpClientFactory: null, logger)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GitHubContentLoader"/> class.</summary>
    /// <param name="repo">The repository point to read from.</param>
    /// <param name="sourcePath">Repository subdirectory to include.</param>
    /// <param name="routePrefix">Local subdirectory the files are mounted under.</param>
    /// <param name="token">Personal access token; empty for unauthenticated requests.</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client; null means the loader owns a short-lived client.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public GitHubContentLoader(GitHubRepoRef repo, PathSegment sourcePath, PathSegment routePrefix, byte[] token, Func<HttpClient>? httpClientFactory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (repo.Owner is not [_, ..] || repo.Repo is not [_, ..] || repo.Reference is not [_, ..])
        {
            throw new ArgumentException("A GitHub repo reference requires a non-empty owner, repository name, and reference.", nameof(repo));
        }

        _repo = repo;
        _sourcePath = sourcePath;
        _routePrefix = routePrefix;
        _token = token;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "github"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        var headers = GitHubUrls.Headers(_token);
        var treeJson = await FetchTreeAsync(headers, cancellationToken).ConfigureAwait(false);
        var entries = GitHubTreeReader.Read(treeJson, in _repo, PrefixBytes(_sourcePath), PrefixBytes(_routePrefix));
        if (entries is [])
        {
            return [];
        }

        var raw = new RawDocumentContentLoader(entries, headers, _httpClientFactory, _logger);
        return await raw.LoadAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the UTF-8 bytes of a path segment, or an empty span when it is empty.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] PrefixBytes(PathSegment segment) =>
        string.IsNullOrEmpty(segment.Value) ? [] : Encoding.UTF8.GetBytes(segment.Value);

    /// <summary>Fetches the recursive git-tree JSON.</summary>
    /// <param name="headers">Request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTF-8 JSON bytes.</returns>
    private async Task<byte[]> FetchTreeAsync((byte[] Name, byte[] Value)[] headers, CancellationToken cancellationToken)
    {
        if (_httpClientFactory is not null)
        {
            return await GetAsync(_httpClientFactory(), headers, cancellationToken).ConfigureAwait(false);
        }

        using HttpClient owned = new();
        return await GetAsync(owned, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Issues the tree-API GET with the supplied headers.</summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="headers">Request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTF-8 response body.</returns>
    private async Task<byte[]> GetAsync(HttpClient client, (byte[] Name, byte[] Value)[] headers, CancellationToken cancellationToken)
    {
        var url = GitHubUrls.TreeApiUrl(in _repo);
        Uri endpoint = new(url.Value, UriKind.Absolute);
        using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
        for (var i = 0; i < headers.Length; i++)
        {
            request.Headers.TryAddWithoutValidation(Encoding.UTF8.GetString(headers[i].Name), Encoding.UTF8.GetString(headers[i].Value));
        }

        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            ContentLoaderLoggingHelper.LogFetchFailed(_logger, "github", url, ex.Message);
            throw new ContentLoaderException(StringCompose.Concat("GitHub tree request to ", url, " failed: ", ex.Message), ex);
        }
    }
}
