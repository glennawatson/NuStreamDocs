// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.Tests;

/// <summary>Coverage for the built-in <see cref="IContentLoader"/> implementations.</summary>
public class LoaderTests
{
    /// <summary>Names the header whose scope is restricted to a loader.</summary>
    private const string LoaderHeaderName = "X-Loader";

    /// <summary>Body accepted by both JSON and raw-document transports.</summary>
    private const string RemoteBody = """[{"slug":"remote","body":"remote body"}]""";

    /// <summary>Gets the route shared by file-loader tests.</summary>
    private static ReadOnlySpan<byte> SlugRoute => "p/{slug}.md"u8;

    /// <summary>Gets the configured request header name as UTF-8 bytes.</summary>
    private static ReadOnlySpan<byte> LoaderHeaderBytes => "X-Loader"u8;

    /// <summary>The file loader reads a JSON array and produces pages.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FileLoaderReadsJson()
    {
        var dir = Directory.CreateTempSubdirectory("nstd-cl-");
        try
        {
            var path = Path.Combine(dir.FullName, "data.json");
            await File.WriteAllTextAsync(path, "[{\"slug\":\"a\",\"title\":\"A\",\"body\":\"Hi A\"}]");
            var loader =
                new FileContentLoader(new(path), ContentMapping.ForRoute(SlugRoute).WithBodyKey("body"u8));

            var pages = await loader.LoadAsync(new(default), CancellationToken.None);

            await Assert.That(pages.Length).IsEqualTo(1);
            await Assert.That(pages[0].RelativePath.Value).IsEqualTo("p/a.md");
            await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).Contains("Hi A");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>The file loader reads a YAML array and produces pages.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FileLoaderReadsYaml()
    {
        var dir = Directory.CreateTempSubdirectory("nstd-cl-");
        try
        {
            var path = Path.Combine(dir.FullName, "data.yaml");
            await File.WriteAllTextAsync(path, "posts:\n  - slug: b\n    title: B\n");
            var loader = new FileContentLoader(
                new(path),
                ContentMapping.ForRoute(SlugRoute).WithCollectionPointer("posts"u8));

            var pages = await loader.LoadAsync(new(default), CancellationToken.None);

            await Assert.That(pages.Length).IsEqualTo(1);
            await Assert.That(pages[0].RelativePath.Value).IsEqualTo("p/b.md");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>The file loader throws when the source file is missing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FileLoaderThrowsWhenMissing()
    {
        var loader =
            new FileContentLoader(
                new(Path.Combine(Path.GetTempPath(), $"nstd-missing-{Guid.NewGuid():N}.json")),
                ContentMapping.ForRoute(SlugRoute));
        await Assert.That(async () => _ = await loader.LoadAsync(new(default), CancellationToken.None))
            .Throws<ContentLoaderException>();
    }

    /// <summary>The HTTP loader GETs a JSON endpoint and maps the response.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HttpLoaderMapsGetResponse()
    {
        const string Json = "{\"results\":[{\"id\":\"one\",\"title\":\"One\",\"body\":\"Body one\"}]}";
        var loader = new HttpContentLoader(
            (UrlPath)"https://api.example.test/things",
            [],
            [],
            ContentMapping.ForRoute("api/{id}.md"u8).WithBodyKey("body"u8).WithCollectionPointer("results"u8),
            static () => StubHttpHandler.ClientReturning(Json),
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("api/one.md");
        await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).Contains("Body one");
    }

    /// <summary>The HTTP loader POSTs a request body when one is supplied (GraphQL).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HttpLoaderPostsWhenBodyGiven()
    {
        StubHttpHandler handler = new(static _ => (HttpStatusCode.OK, "{\"data\":{\"nodes\":[{\"id\":\"q\"}]}}"));
        var loader = new HttpContentLoader(
            (UrlPath)"https://gql.example.test/graphql",
            [.. "{\"query\":\"{ nodes { id } }\"}"u8],
            [],
            ContentMapping.ForRoute("g/{id}.md"u8).WithCollectionPointer("data.nodes"u8),
            () => new(handler),
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Post);
    }

    /// <summary>The HTTP loader wraps transport failures in a <see cref="ContentLoaderException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HttpLoaderWrapsFailures()
    {
        var loader = new HttpContentLoader(
            (UrlPath)"https://api.example.test/things",
            [],
            [],
            ContentMapping.ForRoute("api/{id}.md"u8),
            static () => new(new StubHttpHandler(static _ => (HttpStatusCode.InternalServerError, "boom"))),
            NullLogger.Instance);

        await Assert.That(async () => _ = await loader.LoadAsync(new(default), CancellationToken.None))
            .Throws<ContentLoaderException>();
    }

    /// <summary>The raw-document loader passes each fetched body through verbatim at its route.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RawDocumentLoaderPassesThrough()
    {
        const string Markdown = "---\ntitle: Remote\n---\n\n# Remote page\n";
        var loader = new RawDocumentContentLoader(
            [new((UrlPath)"https://raw.example.test/guide.md", new("guide/remote.md"))],
            [],
            static () => StubHttpHandler.ClientReturning(Markdown),
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("guide/remote.md");
        await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).IsEqualTo(Markdown);
    }

    /// <summary>Default transports remain usable across repeated loads.</summary>
    /// <param name="rawDocument">True to load raw Markdown; false to map JSON.</param>
    /// <param name="cancellationToken">Token that cancels the test.</param>
    /// <returns>A task representing the test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DefaultClientSupportsRepeatedLoads(bool rawDocument, CancellationToken cancellationToken)
    {
        const int LoadCount = 2;
        await using var server = new LoopbackHttpServer(LoadCount, RemoteBody);
        var loader = CreateHttpLoader(rawDocument, server.Endpoint, [], null);

        for (var i = 0; i < LoadCount; i++)
        {
            var pages = await loader.LoadAsync(new(default), cancellationToken);
            await Assert.That(pages.Length).IsEqualTo(1);
            await Assert.That(pages[0].RelativePath.Value).IsEqualTo("remote.md");
            await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).Contains("remote body");
        }

        await Assert.That((await server.Requests).Length).IsEqualTo(LoadCount);
    }

    /// <summary>Default transports isolate configured headers and ignore response cookies.</summary>
    /// <param name="rawDocument">True to load raw Markdown; false to map JSON.</param>
    /// <param name="cancellationToken">Token that cancels the test.</param>
    /// <returns>A task representing the test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DefaultClientKeepsHeadersAndCookiesIsolated(bool rawDocument, CancellationToken cancellationToken)
    {
        const int RequestCount = 3;
        await using var server = new LoopbackHttpServer(RequestCount, RemoteBody);
        var first = CreateHttpLoader(rawDocument, server.Endpoint, [([.. LoaderHeaderBytes], [.. "first"u8])], null);
        var second = CreateHttpLoader(rawDocument, server.Endpoint, [([.. LoaderHeaderBytes], [.. "second"u8])], null);
        var plain = CreateHttpLoader(rawDocument, server.Endpoint, [], null);

        _ = await first.LoadAsync(new(default), cancellationToken);
        _ = await second.LoadAsync(new(default), cancellationToken);
        _ = await plain.LoadAsync(new(default), cancellationToken);

        var requests = await server.Requests;
        await Assert.That(requests[0][LoaderHeaderName]).IsEqualTo("first");
        await Assert.That(requests[1][LoaderHeaderName]).IsEqualTo("second");
        await Assert.That(requests[^1].ContainsKey(LoaderHeaderName)).IsFalse();
        for (var i = 0; i < requests.Length; i++)
        {
            await Assert.That(requests[i].ContainsKey("Cookie")).IsFalse();
        }
    }

    /// <summary>Loader success and failure leave supplied clients usable without changing their default headers.</summary>
    /// <param name="rawDocument">True to load raw Markdown; false to map JSON.</param>
    /// <param name="fails">True to return an unsuccessful status from the transport.</param>
    /// <param name="cancellationToken">Token that cancels the test.</param>
    /// <returns>A task representing the test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SuppliedClientRemainsCallerOwned(bool rawDocument, bool fails, CancellationToken cancellationToken)
    {
        var status = fails ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
        using var fixture = new HttpClientFixture(status, RemoteBody);
        var handler = fixture.Handler;
        var client = fixture.Client;
        client.DefaultRequestHeaders.Add("X-Caller", "caller");
        var endpoint = new Uri("https://transport.example.test/content");
        using var initialResponse = await client.GetAsync(endpoint, cancellationToken);
        await Assert.That(initialResponse.StatusCode).IsEqualTo(status);
        var loader = CreateHttpLoader(rawDocument, endpoint.AbsoluteUri, [([.. LoaderHeaderBytes], [.. "scoped"u8])], () => client);

        if (fails)
        {
            await Assert.That(async () => _ = await loader.LoadAsync(new(default), cancellationToken)).Throws<ContentLoaderException>();
        }
        else
        {
            _ = await loader.LoadAsync(new(default), cancellationToken);
            _ = await loader.LoadAsync(new(default), cancellationToken);
        }

        await Assert.That(handler.Requests[1].Headers.Contains(LoaderHeaderName)).IsTrue();
        await Assert.That(client.DefaultRequestHeaders.Contains(LoaderHeaderName)).IsFalse();
        using var response = await client.GetAsync(endpoint, cancellationToken);
        await Assert.That(response.StatusCode).IsEqualTo(status);
        await Assert.That(handler.Requests[^1].Headers.Contains(LoaderHeaderName)).IsFalse();
        await Assert.That(handler.Requests[^1].Headers.Contains("X-Caller")).IsTrue();
    }

    /// <summary>Builds either HTTP-backed loader with the selected transport.</summary>
    /// <param name="rawDocument">True to load raw Markdown; false to map JSON.</param>
    /// <param name="endpoint">Response endpoint.</param>
    /// <param name="headers">Headers scoped to the loader.</param>
    /// <param name="clientFactory">Optional factory for a caller-owned client.</param>
    /// <returns>The configured loader.</returns>
    private static IContentLoader CreateHttpLoader(
        bool rawDocument,
        UrlPath endpoint,
        (byte[] Name, byte[] Value)[] headers,
        Func<HttpClient>? clientFactory) =>
        rawDocument
            ? new RawDocumentContentLoader([new(endpoint, new("remote.md"))], headers, clientFactory, NullLogger.Instance)
            : new HttpContentLoader(endpoint, [], headers, ContentMapping.ForRoute("{slug}.md"u8).WithBodyKey("body"u8), clientFactory, NullLogger.Instance);
}
