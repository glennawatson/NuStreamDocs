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
    /// <summary>The repository response used to exercise API and raw-file requests together.</summary>
    private const string SingleFileTree = """{"tree":[{"path":"docs/index.md","type":"blob"}]}""";

    /// <summary>The release response used to exercise repeated changelog loads.</summary>
    private const string SingleRelease = """[{"tag_name":"v1","body":"# Remote"}]""";

    /// <summary>Mounts repository documentation under the same local directory.</summary>
    private const string ProductPrefix = "product";

    /// <summary>Gets the repository reference used by loader tests.</summary>
    private static GitHubRepoRef SampleRepo => new([.. "acme"u8], [.. "widgets"u8], [.. "main"u8]);

    /// <summary>The repo loader walks the tree, keeps the Markdown blobs under the source path, and mounts them under the route prefix.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepoLoaderPullsMarkdownUnderPath()
    {
        const string Tree = """
                            {"tree":[
                                {"path":"README.md","type":"blob"},
                                {"path":"docs","type":"tree"},
                                {"path":"docs/index.md","type":"blob"},
                                {"path":"docs/guide/setup.md","type":"blob"},
                                {"path":"docs/logo.png","type":"blob"}],"truncated":false}
                            """;
        using var handler = new StubHandler(static uri =>
            uri.Host == "api.github.com"
                ? (HttpStatusCode.OK, Tree)
                : (HttpStatusCode.OK, $"# {uri.AbsolutePath}"));

        var loader = new GitHubContentLoader(
            SampleRepo,
            (PathSegment)"docs",
            (PathSegment)ProductPrefix,
            [],
            () => handler.Client,
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);
        var routes = Array.ConvertAll(pages, static page => page.RelativePath.Value);
        Array.Sort(routes, StringComparer.Ordinal);

        await Assert.That(string.Join(",", routes)).IsEqualTo("product/guide/setup.md,product/index.md");
        await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).StartsWith("# /");
        await Assert.That(handler.SeenUserAgent).IsTrue();
    }

    /// <summary>A token adds an Authorization header to every request.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TokenAddsAuthorizationHeader()
    {
        using var handler = new StubHandler(static _ => (HttpStatusCode.OK, "{\"tree\":[]}"));
        var loader = new GitHubContentLoader(
            SampleRepo,
            (PathSegment)"docs",
            (PathSegment)ProductPrefix,
            [.. "ghp_secret"u8],
            () => handler.Client,
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
        const int ExpectedPageCount = 2;
        const string Releases = """
                                [{"name":"1.2.0","tag_name":"v1.2.0","prerelease":false,"body":"## Changes\n\n- thing"},
                                 {"name":"1.1.0","tag_name":"v1.1.0","prerelease":false,"body":"older"}]
                                """;
        using var handler = new StubHandler(static _ => (HttpStatusCode.OK, Releases));
        var repo = SampleRepo;

        var loader = new GitHubReleasesContentLoader(
            repo.Owner,
            repo.Repo,
            (PathSegment)"changelog",
            [],
            () => handler.Client,
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(ExpectedPageCount);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("changelog/v1.2.0.md");
        var md = Encoding.UTF8.GetString(pages[0].MarkdownBytes);
        await Assert.That(md).Contains("tag_name: \"v1.2.0\"");
        await Assert.That(md).Contains("## Changes");
        await Assert.That(md).DoesNotContain("body:");
    }

    /// <summary>Repeated loads leave the supplied client usable by its owner.</summary>
    /// <param name="releases">True for releases; false for repository documents.</param>
    /// <param name="cancellationToken">Token that cancels the test.</param>
    /// <returns>A task representing the test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SuppliedClientSupportsRepeatedLoads(bool releases, CancellationToken cancellationToken)
    {
        const int LoadCount = 2;
        using var handler = new StubHandler(RespondWithContent);
        var loader = CreateLoader(releases, [], handler.Client);

        for (var i = 0; i < LoadCount; i++)
        {
            var pages = await loader.LoadAsync(new(default), cancellationToken);
            await Assert.That(pages.Length).IsEqualTo(1);
            await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).Contains("# Remote");
        }

        using var response = await handler.Client.GetAsync(new Uri("https://api.github.com/"), cancellationToken);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    /// <summary>Repository and release tokens stay scoped to each request on a shared caller client.</summary>
    /// <param name="releases">True for releases; false for repository documents.</param>
    /// <param name="cancellationToken">Token that cancels the test.</param>
    /// <returns>A task representing the test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SuppliedClientKeepsTokensIsolated(bool releases, CancellationToken cancellationToken)
    {
        using var handler = new StubHandler(RespondWithContent);
        byte[][] tokens = [[.. "first"u8], [.. "second"u8], []];
        string?[] expected = ["Bearer first", "Bearer second", null];

        for (var i = 0; i < tokens.Length; i++)
        {
            var start = handler.Requests.Count;
            var loader = CreateLoader(releases, tokens[i], handler.Client);
            _ = await loader.LoadAsync(new(default), cancellationToken);

            for (var request = start; request < handler.Requests.Count; request++)
            {
                await Assert.That(handler.Requests[request].Headers.Authorization?.ToString()).IsEqualTo(expected[i]);
                await Assert.That(handler.Requests[request].Headers.UserAgent.ToString()).IsEqualTo("NuStreamDocs-ContentLoader");
            }
        }

        await Assert.That(handler.Client.DefaultRequestHeaders.Authorization).IsNull();
        await Assert.That(handler.Client.DefaultRequestHeaders.UserAgent).IsEmpty();
    }

    /// <summary>Transport failures leave supplied clients usable by their owner.</summary>
    /// <param name="releases">True for releases; false for repository documents.</param>
    /// <param name="rawFailure">True to fail a raw-file fetch after a successful tree request.</param>
    /// <param name="cancellationToken">Token that cancels the test.</param>
    /// <returns>A task representing the test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    public async Task SuppliedClientSurvivesFailure(bool releases, bool rawFailure, CancellationToken cancellationToken)
    {
        using var handler = new StubHandler(uri =>
            rawFailure && uri.Host == "api.github.com"
                ? (HttpStatusCode.OK, SingleFileTree)
                : (HttpStatusCode.InternalServerError, "failure"));
        var loader = CreateLoader(releases, [], handler.Client);

        await Assert.That(async () => _ = await loader.LoadAsync(new(default), cancellationToken)).Throws<ContentLoaderException>();

        using var response = await handler.Client.GetAsync(new Uri("https://raw.githubusercontent.com/"), cancellationToken);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
    }

    /// <summary>Builds a loader that uses the supplied client.</summary>
    /// <param name="releases">True for releases; false for repository documents.</param>
    /// <param name="token">Personal access token, or empty for unauthenticated requests.</param>
    /// <param name="client">Caller-owned client.</param>
    /// <returns>The configured loader.</returns>
    private static IContentLoader CreateLoader(bool releases, byte[] token, HttpClient client)
    {
        var repo = SampleRepo;
        return releases
            ? new GitHubReleasesContentLoader(repo.Owner, repo.Repo, "changelog", token, () => client, NullLogger.Instance)
            : new GitHubContentLoader(repo, "docs", ProductPrefix, token, () => client, NullLogger.Instance);
    }

    /// <summary>Returns content appropriate to the GitHub API or raw-content endpoint.</summary>
    /// <param name="uri">Requested endpoint.</param>
    /// <returns>The response status and body.</returns>
    private static (HttpStatusCode Status, string Body) RespondWithContent(Uri uri) =>
        (HttpStatusCode.OK, uri switch
        {
            { Host: "raw.githubusercontent.com" } => "# Remote",
            var endpoint when endpoint.AbsolutePath.EndsWith("/releases", StringComparison.Ordinal) => SingleRelease,
            _ => SingleFileTree
        });

    /// <summary>A canned-response handler keyed on the request URI; records the GitHub headers it sees.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        /// <summary>Selects the response for each requested endpoint.</summary>
        private readonly Func<Uri, (HttpStatusCode Status, string Body)> _respond;

        /// <summary>Initializes a new instance of the <see cref="StubHandler"/> class.</summary>
        /// <param name="respond">Selects the response for each request.</param>
        public StubHandler(Func<Uri, (HttpStatusCode Status, string Body)> respond) => _respond = respond;

        /// <summary>Gets the reusable client owned by this fixture.</summary>
        public HttpClient Client => field ??= new(this, false);

        /// <summary>Gets the requests observed by the handler.</summary>
        public List<HttpRequestMessage> Requests { get; } = [];

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
            Requests.Add(request);
            if (request.Headers.UserAgent.Count > 0 || request.Headers.Contains("User-Agent"))
            {
                SeenUserAgent = true;
            }

            SeenAuthorization = request.Headers.Authorization?.ToString();

            var (status, body) = _respond(request.RequestUri!);
            return Task.FromResult(
                new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) });
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Client.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
