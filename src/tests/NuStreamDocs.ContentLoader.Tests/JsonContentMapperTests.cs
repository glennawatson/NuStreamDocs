// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace NuStreamDocs.ContentLoader.Tests;

/// <summary>Coverage for <see cref="JsonContentMapper"/> via the public mapping types.</summary>
public class JsonContentMapperTests
{
    /// <summary>A root-level JSON array maps to one page per object, with frontmatter and body.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RootArrayMapsToPages()
    {
        const string Json = "[{\"slug\":\"alpha\",\"title\":\"Alpha\",\"body\":\"# Alpha\\n\\nHello.\"}," +
                            "{\"slug\":\"beta\",\"title\":\"Beta\",\"body\":\"# Beta\"}]";
        var mapping = ContentMapping.ForRoute("posts/{slug}.md"u8).WithBodyKey("body"u8);

        var pages = JsonContentMapper.Map(Encoding.UTF8.GetBytes(Json), mapping, "test"u8, NullLogger.Instance);

        await Assert.That(pages.Length).IsEqualTo(2);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("posts/alpha.md");
        var first = Encoding.UTF8.GetString(pages[0].MarkdownBytes);
        await Assert.That(first).Contains("title: \"Alpha\"");
        await Assert.That(first).Contains("slug: \"alpha\"");
        await Assert.That(first).DoesNotContain("body:");
        await Assert.That(first).Contains("# Alpha\n\nHello.");
        await Assert.That(first.TrimStart()).StartsWith("---");
    }

    /// <summary>A dotted collection pointer navigates into the document before iterating.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CollectionPointerNavigates()
    {
        const string Json = "{\"data\":{\"items\":[{\"id\":\"x\",\"title\":\"X\"}]}}";
        var mapping = ContentMapping.ForRoute("k/{id}.md"u8).WithCollectionPointer("data.items"u8);

        var pages = JsonContentMapper.Map(Encoding.UTF8.GetBytes(Json), mapping, "test"u8, NullLogger.Instance);

        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("k/x.md");
    }

    /// <summary>A frontmatter whitelist keeps only the named fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FrontmatterWhitelist()
    {
        const string Json = "[{\"slug\":\"a\",\"title\":\"A\",\"secret\":\"hidden\"}]";
        var mapping = ContentMapping.ForRoute("p/{slug}.md"u8).WithFrontmatterKeys([[.. "title"u8]]);

        var pages = JsonContentMapper.Map(Encoding.UTF8.GetBytes(Json), mapping, "test"u8, NullLogger.Instance);
        var md = Encoding.UTF8.GetString(pages[0].MarkdownBytes);

        await Assert.That(md).Contains("title: \"A\"");
        await Assert.That(md).DoesNotContain("secret");
        await Assert.That(md).DoesNotContain("slug:");
    }

    /// <summary>An entry whose route template references a missing field is skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EntryWithoutRouteFieldIsSkipped()
    {
        const string Json = "[{\"slug\":\"good\",\"title\":\"G\"},{\"title\":\"no-slug\"}]";
        var mapping = ContentMapping.ForRoute("p/{slug}.md"u8);

        var pages = JsonContentMapper.Map(Encoding.UTF8.GetBytes(Json), mapping, "test"u8, NullLogger.Instance);

        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("p/good.md");
    }

    /// <summary>A collection pointer that does not resolve to an array yields no pages.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BadCollectionPointerYieldsNothing()
    {
        const string Json = "{\"data\":{\"items\":\"not-an-array\"}}";
        var mapping = ContentMapping.ForRoute("k/{id}.md"u8).WithCollectionPointer("data.items"u8);

        var pages = JsonContentMapper.Map(Encoding.UTF8.GetBytes(Json), mapping, "test"u8, NullLogger.Instance);
        await Assert.That(pages).IsEmpty();
    }

    /// <summary>Malformed JSON in the collection array throws a <see cref="ContentLoaderException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MalformedJsonThrows()
    {
        var mapping = ContentMapping.ForRoute("p/{slug}.md"u8);
        var json = Encoding.UTF8.GetBytes("[ this is not json");
        await Assert.That(() => _ = JsonContentMapper.Map(json, mapping, "test"u8, NullLogger.Instance))
            .Throws<ContentLoaderException>();
    }

    /// <summary>An empty route template is rejected when the mapping is validated.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyRouteTemplateRejected()
    {
        var mapping = new ContentMapping([], [], [], []);
        await Assert.That(mapping.Validate).Throws<ArgumentException>();
    }

    /// <summary>The fluent mutators compose without mutating the source mapping.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MappingMutatorsCompose()
    {
        var baseMapping = ContentMapping.ForRoute("p/{id}.md"u8);
        var derived = baseMapping.WithBodyKey("content"u8).WithCollectionPointer("a.b"u8);

        await Assert.That(Encoding.UTF8.GetString(derived.BodyKey)).IsEqualTo("content");
        await Assert.That(Encoding.UTF8.GetString(derived.CollectionPointer)).IsEqualTo("a.b");
        await Assert.That(baseMapping.BodyKey).IsEmpty();
    }
}
