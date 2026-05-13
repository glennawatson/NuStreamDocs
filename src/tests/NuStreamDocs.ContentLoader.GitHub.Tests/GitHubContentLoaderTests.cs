// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.GitHub.Tests;

/// <summary>Coverage for <see cref="GitHubContentLoader"/> and <see cref="GitHubReleasesContentLoader"/> against canned API responses.</summary>
public class GitHubContentLoaderTests
{
    /// <summary>The repo loader walks the tree, keeps the Markdown blobs under the source path, and mounts them under the route prefix.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepoLoaderPullsMarkdownUnderPath()
    {
        const string Tree = "{\"tree\":[" +
                            "{\"path\":\"README.md\",\"type\":\"blob\"}," +
                            "{\"path\":\"docs\",\"type\":\"tree\"}," +
                            "{\"path\":\"docs/index.md\",\"type\":\"blob\"}," +
                            "{\"path\":\"docs/guide/setup.md\",\"type\":\"blob\"}," +
                            "{\"path\":\"docs/logo.png\",\"type\":\"blob\"}],\"truncated\":false}";
        StubHandler handler = new(uri =>
            uri.Host == "api.github.com"
                ? (HttpStatusCode.OK, Tree)
                : (HttpStatusCode.OK, "# " + uri.AbsolutePath));

        var loader = new GitHubContentLoader(
            new([.. "acme"u8], [.. "widgets"u8], [.. "main"u8]),
            (PathSegment)"docs",
            (PathSegment)"product",
            [],
            () => new(handler),
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);
        var routes = pages.Select(p => p.RelativePath.Value).OrderBy(static p => p, StringComparer.Ordinal).ToArray();

        await Assert.That(string.Join(",", routes)).IsEqualTo("product/guide/setup.md,product/index.md");
        await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).StartsWith("# /");
        await Assert.That(handler.SeenUserAgent).IsTrue();
    }

    /// <summary>A token adds an Authorization header to every request.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TokenAddsAuthorizationHeader()
    {
        StubHandler handler = new(_ => (HttpStatusCode.OK, "{\"tree\":[]}"));
        var loader = new GitHubContentLoader(
            new([.. "acme"u8], [.. "widgets"u8], [.. "main"u8]),
            (PathSegment)"docs",
            (PathSegment)"product",
            [.. "ghp_secret"u8],
            () => new(handler),
            NullLogger.Instance);

        _ = await loader.LoadAsync(new(default), CancellationToken.None);
        await Assert.That(handler.SeenAuthorization).IsEqualTo("Bearer ghp_secret");
    }

    /// <summary>An invalid repo reference is rejected by the constructor.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InvalidRepoRefRejected() =>
        await Assert.That(static () => new GitHubContentLoader(new([], [.. "r"u8], [.. "main"u8]), default, default))
            .Throws<ArgumentException>();

    /// <summary>The releases loader maps each release to a tag-routed changelog page with the notes as the body.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReleasesLoaderProducesChangelogPages()
    {
        const string Releases =
            "[{\"name\":\"1.2.0\",\"tag_name\":\"v1.2.0\",\"prerelease\":false,\"body\":\"## Changes\\n\\n- thing\"}," +
            "{\"name\":\"1.1.0\",\"tag_name\":\"v1.1.0\",\"prerelease\":false,\"body\":\"older\"}]";
        StubHandler handler = new(_ => (HttpStatusCode.OK, Releases));

        var loader = new GitHubReleasesContentLoader(
            [.. "acme"u8],
            [.. "widgets"u8],
            (PathSegment)"changelog",
            [],
            () => new(handler),
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(2);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("changelog/v1.2.0.md");
        var md = Encoding.UTF8.GetString(pages[0].MarkdownBytes);
        await Assert.That(md).Contains("tag_name: \"v1.2.0\"");
        await Assert.That(md).Contains("## Changes");
        await Assert.That(md).DoesNotContain("body:");
    }

    /// <summary>A canned-response handler keyed on the request URI; records the GitHub headers it sees.</summary>
    private sealed class StubHandler(Func<Uri, (HttpStatusCode Status, string Body)> respond) : HttpMessageHandler
    {
        /// <summary>Gets a value indicating whether a <c>User-Agent</c> header was seen.</summary>
        public bool SeenUserAgent { get; private set; }

        /// <summary>Gets the last <c>Authorization</c> header value seen, or null.</summary>
        public string? SeenAuthorization { get; private set; }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            if (request.Headers.UserAgent.Count > 0 || request.Headers.Contains("User-Agent"))
            {
                SeenUserAgent = true;
            }

            if (request.Headers.TryGetValues("Authorization", out var auth))
            {
                SeenAuthorization = string.Concat(auth);
            }

            var (status, body) = respond(request.RequestUri!);
            return Task.FromResult(
                new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) });
        }
    }
}
