// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Config.MkDocs;
using NuStreamDocs.ContentLoader;
using NuStreamDocs.ContentLoader.Feed;
using NuStreamDocs.ContentLoader.GitHub;
using NuStreamDocs.ContentLoader.OpenApi;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// CPU + allocation cost of the pure parse/render cores behind the content loaders — the work that
/// runs once per build per source, scaling with the number of records. Excludes network/disk I/O,
/// which the loaders layer on top.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class ContentLoaderBenchmarks
{
    /// <summary>Small record count — a modest CMS collection / feed.</summary>
    private const int SmallRecordCount = 16;

    /// <summary>Large record count — a sizeable API surface / archive feed.</summary>
    private const int LargeRecordCount = 256;

    /// <summary>Approximate bytes per synthesized record; used only to pre-size builders.</summary>
    private const int ApproxBytesPerRecord = 256;

    /// <summary>Number of subdirectories / tags the tree and OpenAPI fixtures spread records across.</summary>
    private const int GroupCount = 8;

    /// <summary>Number of HTTP methods the OpenAPI fixture cycles through.</summary>
    private const int MethodCount = 4;

    /// <summary>Loader name passed to <see cref="JsonContentMapper"/> for diagnostics.</summary>
    private static readonly byte[] LoaderName = [.. "bench"u8];

    /// <summary>Mapping used for the JSON-collection benchmark.</summary>
    private static readonly ContentMapping Mapping =
        ContentMapping.ForRoute("posts/{slug}.md"u8).WithBodyKey("body"u8);

    /// <summary>Mapping used for the YAML-collection benchmark (array under a key, no body).</summary>
    private static readonly ContentMapping YamlMapping =
        ContentMapping.ForRoute("posts/{slug}.md"u8).WithCollectionPointer("posts"u8);

    /// <summary>GitHub repo reference used to build raw-content URLs in the tree benchmark.</summary>
    private static readonly GitHubRepoRef Repo = new([.. "acme"u8], [.. "widgets"u8], [.. "main"u8]);

    /// <summary>HTTP method names the OpenAPI fixture cycles through.</summary>
    private static readonly string[] HttpMethods = ["get", "post", "put", "delete"];

    /// <summary>JSON array of <see cref="Records"/> objects.</summary>
    private byte[] _json = [];

    /// <summary>YAML document with a <c>posts:</c> list of <see cref="Records"/> entries.</summary>
    private byte[] _yaml = [];

    /// <summary>RSS 2.0 feed with <see cref="Records"/> items.</summary>
    private byte[] _rss = [];

    /// <summary>Atom feed with <see cref="Records"/> entries.</summary>
    private byte[] _atom = [];

    /// <summary>GitHub git-tree response with <see cref="Records"/> Markdown blobs.</summary>
    private byte[] _tree = [];

    /// <summary>OpenAPI document with <see cref="Records"/> operations spread across <see cref="GroupCount"/> tags.</summary>
    private byte[] _openApi = [];

    /// <summary>Gets or sets the number of records in each synthesized source.</summary>
    [Params(SmallRecordCount, LargeRecordCount)]
    public int Records { get; set; }

    /// <summary>Builds the source fixtures for the current record count.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _json = Encoding.UTF8.GetBytes(BuildJsonArray(Records));
        _yaml = Encoding.UTF8.GetBytes(BuildYaml(Records));
        _rss = Encoding.UTF8.GetBytes(BuildRss(Records));
        _atom = Encoding.UTF8.GetBytes(BuildAtom(Records));
        _tree = Encoding.UTF8.GetBytes(BuildTree(Records));
        _openApi = Encoding.UTF8.GetBytes(BuildOpenApi(Records));
    }

    /// <summary>Maps the JSON collection into synthetic pages.</summary>
    /// <returns>The number of pages produced.</returns>
    [Benchmark]
    public int JsonMap() => JsonContentMapper.Map(_json, Mapping, LoaderName, NullLogger.Instance).Length;

    /// <summary>The local-file YAML path: convert YAML to JSON, then map (the work <c>FileContentLoader</c> does minus the disk read).</summary>
    /// <returns>The number of pages produced.</returns>
    [Benchmark]
    public int YamlMap()
    {
        ArrayBufferWriter<byte> json = new(_yaml.Length);
        using (Utf8JsonWriter writer = new(json))
        {
            YamlToJson.Convert(_yaml, writer);
        }

        return JsonContentMapper.Map(json.WrittenSpan.ToArray(), YamlMapping, LoaderName, NullLogger.Instance).Length;
    }

    /// <summary>Parses the RSS feed into items.</summary>
    /// <returns>The number of items parsed.</returns>
    [Benchmark]
    public int RssRead() => RssAtomReader.Read(_rss).Length;

    /// <summary>Parses the Atom feed into entries.</summary>
    /// <returns>The number of entries parsed.</returns>
    [Benchmark]
    public int AtomRead() => RssAtomReader.Read(_atom).Length;

    /// <summary>Resolves the GitHub tree response into raw-document entries.</summary>
    /// <returns>The number of entries.</returns>
    [Benchmark]
    public int GitHubTreeRead() => GitHubTreeReader.Read(_tree, in Repo, "docs"u8, "product"u8).Length;

    /// <summary>Renders the OpenAPI document into per-tag reference pages.</summary>
    /// <returns>The number of pages produced.</returns>
    [Benchmark]
    public int OpenApiBuild() => OpenApiPageBuilder.Build(_openApi, "reference"u8).Length;

    /// <summary>Decimal text of <paramref name="i"/>.</summary>
    /// <param name="i">Value.</param>
    /// <returns>The text.</returns>
    private static string N(int i) => i.ToString(CultureInfo.InvariantCulture);

    /// <summary>Builds a JSON array of <paramref name="count"/> post objects.</summary>
    /// <param name="count">Record count.</param>
    /// <returns>JSON text.</returns>
    private static string BuildJsonArray(int count)
    {
        StringBuilder sb = new(count * ApproxBytesPerRecord);
        sb.Append('[');
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("{\"slug\":\"post-").Append(N(i))
                .Append("\",\"title\":\"Post ").Append(N(i))
                .Append("\",\"date\":\"2026-05-01\",\"tags\":[\"a\",\"b\"],\"draft\":false,\"body\":\"# Post ")
                .Append(N(i))
                .Append("\\n\\nLorem ipsum dolor sit amet, consectetur adipiscing elit.\"}");
        }

        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>Builds a mapping-rooted YAML document with a <c>posts:</c> list of <paramref name="count"/> entries.</summary>
    /// <param name="count">Record count.</param>
    /// <returns>YAML text.</returns>
    private static string BuildYaml(int count)
    {
        StringBuilder sb = new(count * ApproxBytesPerRecord);
        sb.Append("posts:\n");
        for (var i = 0; i < count; i++)
        {
            sb.Append("  - slug: post-").Append(N(i)).Append('\n')
                .Append("    title: Post ").Append(N(i)).Append('\n')
                .Append("    date: 2026-05-01\n")
                .Append("    draft: false\n");
        }

        return sb.ToString();
    }

    /// <summary>Builds an RSS 2.0 feed with <paramref name="count"/> items.</summary>
    /// <param name="count">Item count.</param>
    /// <returns>RSS XML.</returns>
    private static string BuildRss(int count)
    {
        StringBuilder sb = new(count * ApproxBytesPerRecord);
        sb.Append("<?xml version=\"1.0\"?><rss version=\"2.0\"><channel><title>Bench</title>");
        for (var i = 0; i < count; i++)
        {
            sb.Append("<item><title>Item ").Append(N(i))
                .Append("</title><link>https://blog.test/").Append(N(i))
                .Append("</link><pubDate>Mon, 04 May 2026 12:00:00 GMT</pubDate><guid>https://blog.test/").Append(N(i))
                .Append("</guid><description>&lt;p&gt;Body ").Append(N(i)).Append("&lt;/p&gt;</description></item>");
        }

        sb.Append("</channel></rss>");
        return sb.ToString();
    }

    /// <summary>Builds an Atom feed with <paramref name="count"/> entries.</summary>
    /// <param name="count">Entry count.</param>
    /// <returns>Atom XML.</returns>
    private static string BuildAtom(int count)
    {
        StringBuilder sb = new(count * ApproxBytesPerRecord);
        sb.Append("<?xml version=\"1.0\"?><feed xmlns=\"http://www.w3.org/2005/Atom\"><title>Bench</title>");
        for (var i = 0; i < count; i++)
        {
            sb.Append("<entry><title>Entry ").Append(N(i))
                .Append("</title><link rel=\"alternate\" href=\"https://a.test/").Append(N(i))
                .Append("\"/><updated>2026-05-04T12:00:00Z</updated><id>urn:uuid:").Append(N(i))
                .Append("</id><content type=\"html\">&lt;p&gt;Body ").Append(N(i))
                .Append("&lt;/p&gt;</content></entry>");
        }

        sb.Append("</feed>");
        return sb.ToString();
    }

    /// <summary>Builds a GitHub git-tree response with <paramref name="count"/> Markdown blobs under <c>docs/</c>.</summary>
    /// <param name="count">Blob count.</param>
    /// <returns>JSON text.</returns>
    private static string BuildTree(int count)
    {
        StringBuilder sb = new(count * ApproxBytesPerRecord);
        sb.Append("{\"sha\":\"x\",\"tree\":[");
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("{\"path\":\"docs/section").Append(i % GroupCount)
                .Append("/page").Append(N(i))
                .Append(".md\",\"type\":\"blob\",\"sha\":\"deadbeef").Append(N(i)).Append("\"}");
        }

        sb.Append(",{\"path\":\"docs\",\"type\":\"tree\",\"sha\":\"t\"},")
            .Append("{\"path\":\"docs/logo.png\",\"type\":\"blob\",\"sha\":\"p\"}]}");
        return sb.ToString();
    }

    /// <summary>Builds an OpenAPI 3.x document with <paramref name="count"/> operations across <see cref="GroupCount"/> tags.</summary>
    /// <param name="count">Operation count.</param>
    /// <returns>JSON text.</returns>
    private static string BuildOpenApi(int count)
    {
        StringBuilder sb = new(count * ApproxBytesPerRecord);
        sb.Append("{\"openapi\":\"3.0.0\",\"info\":{\"title\":\"Bench\"},\"paths\":{");
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("\"/resource").Append(N(i)).Append("/{id}\":{\"").Append(HttpMethods[i % MethodCount])
                .Append("\":{\"tags\":[\"tag").Append(i % GroupCount)
                .Append("\"],\"summary\":\"Operation ").Append(N(i))
                .Append(
                    "\",\"parameters\":[{\"name\":\"id\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"string\"},\"description\":\"The id\"}],")
                .Append("\"requestBody\":{\"content\":{\"application/json\":{}}},")
                .Append("\"responses\":{\"200\":{\"description\":\"OK\"},\"404\":{\"description\":\"Missing\"}}}}");
        }

        sb.Append("}}");
        return sb.ToString();
    }
}
