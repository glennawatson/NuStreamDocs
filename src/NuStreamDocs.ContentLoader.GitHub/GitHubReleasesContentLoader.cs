// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.GitHub;

/// <summary>
/// Turns a GitHub repository's releases into changelog pages — one page per release, routed by tag,
/// with the release notes as the body and <c>name</c> / <c>tag_name</c> / <c>published_at</c> /
/// <c>prerelease</c> / <c>html_url</c> as frontmatter.
/// </summary>
public sealed class GitHubReleasesContentLoader : IContentLoader
{
    /// <summary>Frontmatter fields kept from each release object.</summary>
    private static readonly byte[][] ReleaseFrontmatterKeys =
    [
        [.. "name"u8], [.. "tag_name"u8], [.. "published_at"u8], [.. "prerelease"u8], [.. "draft"u8], [.. "html_url"u8]
    ];

    /// <summary>Repository owner.</summary>
    private readonly byte[] _owner;

    /// <summary>Repository name.</summary>
    private readonly byte[] _repo;

    /// <summary>Subdirectory the changelog pages are placed under (e.g. <c>changelog</c>).</summary>
    private readonly PathSegment _routePrefix;

    /// <summary>Personal access token; empty for unauthenticated requests.</summary>
    private readonly byte[] _token;

    /// <summary>HTTP client factory; null means the loader owns a short-lived client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="GitHubReleasesContentLoader"/> class for an unauthenticated pull.</summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="routePrefix">Subdirectory the changelog pages are placed under.</param>
    public GitHubReleasesContentLoader(byte[] owner, byte[] repo, PathSegment routePrefix)
        : this(owner, repo, routePrefix, [], httpClientFactory: null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GitHubReleasesContentLoader"/> class.</summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="routePrefix">Subdirectory the changelog pages are placed under.</param>
    /// <param name="token">Personal access token; empty for unauthenticated requests.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public GitHubReleasesContentLoader(byte[] owner, byte[] repo, PathSegment routePrefix, byte[] token, ILogger logger)
        : this(owner, repo, routePrefix, token, httpClientFactory: null, logger)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GitHubReleasesContentLoader"/> class.</summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="routePrefix">Subdirectory the changelog pages are placed under.</param>
    /// <param name="token">Personal access token; empty for unauthenticated requests.</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client; null means the loader owns a short-lived client.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public GitHubReleasesContentLoader(byte[] owner, byte[] repo, PathSegment routePrefix, byte[] token, Func<HttpClient>? httpClientFactory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (owner is not [_, ..] || repo is not [_, ..])
        {
            throw new ArgumentException("A non-empty owner and repository name are required.");
        }

        ArgumentException.ThrowIfNullOrEmpty(routePrefix.Value);
        _owner = owner;
        _repo = repo;
        _routePrefix = routePrefix;
        _token = token;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "github-releases"u8;

    /// <inheritdoc/>
    public ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        GitHubRepoRef repoRef = new(_owner, _repo, [.. "HEAD"u8]);
        var mapping = new ContentMapping(RouteTemplate(_routePrefix), [.. "body"u8], [], ReleaseFrontmatterKeys);
        var inner = new HttpContentLoader(
            GitHubUrls.ReleasesApiUrl(in repoRef),
            [],
            GitHubUrls.Headers(_token),
            mapping,
            _httpClientFactory,
            _logger);
        return inner.LoadAsync(context, cancellationToken);
    }

    /// <summary>Builds the route template <c>{prefix}/{tag_name}.md</c> as UTF-8 bytes.</summary>
    /// <param name="prefix">Route prefix.</param>
    /// <returns>The template bytes.</returns>
    private static byte[] RouteTemplate(PathSegment prefix)
    {
        ArrayBufferWriter<byte> writer = new(prefix.Value.Length + 16);
        Encoding.UTF8.GetBytes(prefix.Value.AsSpan(), writer);
        writer.Write("/{tag_name}.md"u8);
        return writer.WrittenSpan.ToArray();
    }
}
