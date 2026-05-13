// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.OpenApi.Tests;

/// <summary>Coverage for <see cref="OpenApiContentLoader"/> and <see cref="OpenApiPageBuilder"/>.</summary>
public class OpenApiContentLoaderTests
{
    /// <summary>A two-operation spec under one tag.</summary>
    private const string Spec =
        "{\"openapi\":\"3.0.0\",\"info\":{\"title\":\"Demo\"},\"paths\":{" +
        "\"/users/{id}\":{" +
        "\"get\":{\"tags\":[\"Users\"],\"summary\":\"Get a user\",\"parameters\":[{\"name\":\"id\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"string\"},\"description\":\"User id\"}]," +
        "\"responses\":{\"200\":{\"description\":\"OK\"},\"404\":{\"description\":\"Not found\"}}}," +
        "\"post\":{\"tags\":[\"Users\"],\"summary\":\"Replace a user\",\"requestBody\":{\"content\":{\"application/json\":{}}},\"responses\":{\"200\":{\"description\":\"OK\"}}}}}}";

    /// <summary>Reads a JSON spec from a temp file and produces one page per tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsJsonSpecFile()
    {
        var dir = Directory.CreateTempSubdirectory("nstd-oas-");
        try
        {
            var path = Path.Combine(dir.FullName, "api.json");
            await File.WriteAllTextAsync(path, Spec);
            var loader = new OpenApiContentLoader(new FilePath(path), (PathSegment)"reference");

            var pages = await loader.LoadAsync(new(default), CancellationToken.None);

            await Assert.That(pages.Length).IsEqualTo(1);
            await Assert.That(pages[0].RelativePath.Value).IsEqualTo("reference/users.md");
            var md = Encoding.UTF8.GetString(pages[0].MarkdownBytes);
            await Assert.That(md).Contains("title: \"Users\"");
            await Assert.That(md).Contains("## GET /users/{id}");
            await Assert.That(md).Contains("## POST /users/{id}");
            await Assert.That(md).Contains("| id | path | string | yes | User id |");
            await Assert.That(md).Contains("**Request body:** `application/json`");
            await Assert.That(md).Contains("- `404` — Not found");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>Reads an equivalent YAML spec.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsYamlSpecFile()
    {
        var dir = Directory.CreateTempSubdirectory("nstd-oas-");
        try
        {
            var path = Path.Combine(dir.FullName, "api.yaml");
            const string Yaml = "openapi: 3.0.0\npaths:\n  /ping:\n    get:\n      tags:\n        - Health\n"
                                + "      summary: Ping\n      responses:\n        '200':\n          description: OK\n";
            await File.WriteAllTextAsync(path, Yaml);
            var loader = new OpenApiContentLoader(new FilePath(path), (PathSegment)"api");

            var pages = await loader.LoadAsync(new(default), CancellationToken.None);

            await Assert.That(pages.Length).IsEqualTo(1);
            await Assert.That(pages[0].RelativePath.Value).IsEqualTo("api/health.md");
            await Assert.That(Encoding.UTF8.GetString(pages[0].MarkdownBytes)).Contains("## GET /ping");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>A spec with no paths yields no pages.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpecWithoutPathsYieldsNothing()
    {
        var pages = OpenApiPageBuilder.Build(Encoding.UTF8.GetBytes("{\"openapi\":\"3.0.0\"}"), "x"u8);
        await Assert.That(pages).IsEmpty();
    }

    /// <summary>Untagged operations land on a <c>default</c> page.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UntaggedOperationsGoToDefault()
    {
        const string SpecText =
            "{\"paths\":{\"/x\":{\"get\":{\"summary\":\"x\",\"responses\":{\"200\":{\"description\":\"OK\"}}}}}}";
        var pages = OpenApiPageBuilder.Build(Encoding.UTF8.GetBytes(SpecText), "ref"u8);
        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("ref/default.md");
    }

    /// <summary>Constructing with neither a file nor a URL is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RequiresExactlyOneSource() =>
        await Assert.That(static () => new OpenApiContentLoader(default(FilePath), default))
            .Throws<ArgumentException>();
}
