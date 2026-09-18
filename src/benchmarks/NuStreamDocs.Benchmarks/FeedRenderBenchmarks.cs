// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.ContentLoader.Feed;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-item cost of the feed-loader's render helpers — slugging an item title into a route segment
/// (<c>Slug.FromBytes</c>) and assembling one synthetic page's Markdown with YAML-escaped frontmatter
/// (<c>FeedMarkdown.Build</c>). These run once per feed item on top of the parse measured by the
/// <c>RssRead</c> / <c>AtomRead</c> benchmarks.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class FeedRenderBenchmarks
{
    /// <summary>A representative feed item with a short HTML body.</summary>
    private static readonly FeedItem Item = new(
        [.. "Announcing the \"New\" Release"u8],
        [.. "https://blog.example.test/announcing-the-new-release"u8],
        [.. "Mon, 04 May 2026 12:00:00 GMT"u8],
        [.. "https://blog.example.test/announcing-the-new-release"u8],
        [.. "<p>We are excited to announce the new release. It includes <b>many</b> improvements and fixes.</p>"u8]);

    /// <summary>Gets a clean title consisting mostly of letters and spaces.</summary>
    private static ReadOnlySpan<byte> SimpleTitle => "Announcing the New Release"u8;

    /// <summary>Gets a punctuation-heavy title for the hyphen-collapse path.</summary>
    private static ReadOnlySpan<byte> MessyTitle => "Re: [URGENT!!!] \"Q3\" review & sign-off — (v2.0)"u8;

    /// <summary>Gets the feed URL stamped into each page's frontmatter.</summary>
    private static ReadOnlySpan<byte> FeedUrl => "https://blog.example.test/feed.xml"u8;

    /// <summary>Slugs a clean title.</summary>
    /// <returns>The slug length.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int SlugifySimple() => Slug.FromBytes(SimpleTitle).Length;

    /// <summary>Slugs a punctuation-heavy title.</summary>
    /// <returns>The slug length.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int SlugifyMessy() => Slug.FromBytes(MessyTitle).Length;

    /// <summary>Assembles one synthetic page's Markdown from a feed item.</summary>
    /// <returns>The Markdown byte length.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int BuildItemMarkdown() => FeedMarkdown.Build(in Item, FeedUrl).Length;
}
