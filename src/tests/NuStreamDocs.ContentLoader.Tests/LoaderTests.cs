// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.Tests;

/// <summary>Coverage for the built-in <see cref="IContentLoader"/> implementations.</summary>
public class LoaderTests
{
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
            var loader = new FileContentLoader(new FilePath(path), ContentMapping.ForRoute("p/{slug}.md"u8).WithBodyKey("body"u8));

            var pages = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None);

            await Assert.That(pages.Length).IsEqualTo(1);
            await Assert.That(pages[0].RelativePath.Value).IsEqualTo("p/a.md");
            await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).Contains("Hi A");
        }
        finally
        {
            dir.Delete(recursive: true);
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
            var loader = new FileContentLoader(new FilePath(path), ContentMapping.ForRoute("p/{slug}.md"u8).WithCollectionPointer("posts"u8));

            var pages = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None);

            await Assert.That(pages.Length).IsEqualTo(1);
            await Assert.That(pages[0].RelativePath.Value).IsEqualTo("p/b.md");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>The file loader throws when the source file is missing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FileLoaderThrowsWhenMissing()
    {
        var loader = new FileContentLoader(new FilePath(Path.Combine(Path.GetTempPath(), "nstd-missing-" + Guid.NewGuid().ToString("N") + ".json")), ContentMapping.ForRoute("p/{slug}.md"u8));
        await Assert.That(async () => _ = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None)).Throws<ContentLoaderException>();
    }

    /// <summary>The HTTP loader GETs a JSON endpoint and maps the response.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HttpLoaderMapsGetResponse()
    {
        const string json = "{\"results\":[{\"id\":\"one\",\"title\":\"One\",\"body\":\"Body one\"}]}";
        var loader = new HttpContentLoader(
            (UrlPath)"https://api.example.test/things",
            [],
            [],
            ContentMapping.ForRoute("api/{id}.md"u8).WithBodyKey("body"u8).WithCollectionPointer("results"u8),
            () => StubHttpHandler.ClientReturning(json),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var pages = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("api/one.md");
        await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).Contains("Body one");
    }

    /// <summary>The HTTP loader POSTs a request body when one is supplied (GraphQL).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HttpLoaderPostsWhenBodyGiven()
    {
        StubHttpHandler handler = new(_ => (System.Net.HttpStatusCode.OK, "{\"data\":{\"nodes\":[{\"id\":\"q\"}]}}"));
        var loader = new HttpContentLoader(
            (UrlPath)"https://gql.example.test/graphql",
            [.. "{\"query\":\"{ nodes { id } }\"}"u8],
            [],
            ContentMapping.ForRoute("g/{id}.md"u8).WithCollectionPointer("data.nodes"u8),
            () => new HttpClient(handler),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var pages = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None);

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
            () => new HttpClient(new StubHttpHandler(_ => (System.Net.HttpStatusCode.InternalServerError, "boom"))),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await Assert.That(async () => _ = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None)).Throws<ContentLoaderException>();
    }

    /// <summary>The raw-document loader passes each fetched body through verbatim at its route.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RawDocumentLoaderPassesThrough()
    {
        const string markdown = "---\ntitle: Remote\n---\n\n# Remote page\n";
        var loader = new RawDocumentContentLoader(
            [new((UrlPath)"https://raw.example.test/guide.md", new FilePath("guide/remote.md"))],
            [],
            () => StubHttpHandler.ClientReturning(markdown),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var pages = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("guide/remote.md");
        await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).IsEqualTo(markdown);
    }
}
