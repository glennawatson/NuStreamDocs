// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Videos;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput and allocation benchmarks for <see cref="VideoPlugin"/>, <see cref="VideoUrlParser"/>, and <see cref="VideoRewriter"/>.</summary>
[DebuggerDisplay("VideoBenchmarks: multiProviderLen={_multiProviderPage.Length}")]
[ShortRunJob]
[MemoryDiagnoser]
public class VideoBenchmarks
{
    /// <summary>Repetition count for prose and link-dense non-video pages.</summary>
    private const int PageLineRepetitions = 30;

    /// <summary>Pre-allocated output buffer capacity so the benchmark measures rewriter allocations rather than sink growth.</summary>
    private const int ReusableWriterCapacity = 16_384;

    /// <summary>Configured plugin instance with default options.</summary>
    private readonly VideoPlugin _plugin = new();

    /// <summary>Reusable output sink reset before each rewrite benchmark call.</summary>
    private readonly ArrayBufferWriter<byte> _reusableWriter = new(ReusableWriterCapacity);

    /// <summary>Resolved Twitch parent host bytes.</summary>
    private readonly byte[] _twitchParent = [.. "docs.reactiveui.net"u8];

    /// <summary>Prose-only page with zero URLs.</summary>
    private byte[] _proseOnlyPage = [];

    /// <summary>Documentation page dense with non-video GitHub and Microsoft Learn URLs.</summary>
    private byte[] _nonVideoLinksPage = [];

    /// <summary>Multi-provider Markdown page mixing prose, code fences, inline links, and 12 video embeds.</summary>
    private byte[] _multiProviderPage = [];

    /// <summary>Representative URLs across every supported video provider.</summary>
    private byte[][] _providerUrls = [];

    /// <summary>Builds the UTF-8 benchmark inputs once before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _proseOnlyPage = Repeat(
            "ReactiveUI composes observable streams into testable view models across desktop, mobile, and web apps.\n\n"u8,
            PageLineRepetitions);

        _nonVideoLinksPage = Repeat(
            "See [ReactiveUI](https://github.com/reactiveui/ReactiveUI) and [Docs](https://learn.microsoft.com/dotnet/fundamentals) for API guidance.\n\n"u8,
            PageLineRepetitions);

        _multiProviderPage =
        [
            .. """
               # Video Tutorials

               Read the [API guide](https://github.com/reactiveui/ReactiveUI) or watch [the intro](https://www.youtube.com/watch?v=dQw4w9WgXcQ) inline.

               ```markdown
               https://www.youtube.com/watch?v=dQw4w9WgXcQ
               ![Ignored in fence](https://vimeo.com/76979871)
               ```

               https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=1m30s

               ![Vimeo Architecture](https://vimeo.com/76979871/8f7e6d5c4b)

               https://onedrive.live.com/embed?resid=12345!678&authkey=!AbCdEf

               https://contoso.sharepoint.com/sites/eng/_layouts/15/stream.aspx?id=%2Fsites%2Feng%2Fdemo.mp4

               ![Azure Indexer](https://www.videoindexer.ai/accounts/00000000-0000-0000-0000-000000000000/videos/vid12345)

               https://myaccount.blob.core.windows.net/videos/intro.mp4?sv=2025-01-01&sig=abc

               https://www.loom.com/share/8a9b0c1d2e3f4a5b

               https://www.dailymotion.com/video/x84sh87_launch-keynote

               https://www.twitch.tv/videos/1987654321

               https://reactiveui.wistia.com/medias/wistia987abc

               https://customer-f33zs165nr7gyfy4.cloudflarestream.com/6b9e68b07dfee8cc2d116e4c51d6a957/watch

               ![Release Recording](https://drive.google.com/file/d/1aBcDeFgHiJkLmNoPqRsTuVwXyZ/view)
               """u8
        ];

        _providerUrls = CreateProviderUrls();
    }

    /// <summary>Fast-reject pre-filter on a prose page with zero URLs.</summary>
    /// <returns><c>false</c> for non-video content.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark(Baseline = true)]
    public bool ProbeProseNoUrls() => _plugin.NeedsRewrite(_proseOnlyPage);

    /// <summary>Fast-reject pre-filter on a page containing 60 non-video GitHub and Microsoft Learn URLs.</summary>
    /// <returns><c>false</c> for non-video hosts.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public bool ProbeNonVideoLinksOnly() => _plugin.NeedsRewrite(_nonVideoLinksPage);

    /// <summary>Zero-allocation URL parsing across 16 URLs spanning all supported video platforms.</summary>
    /// <returns>Number of matched video URLs.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int ParseAllProviders()
    {
        var count = 0;
        var urls = _providerUrls;
        for (var i = 0; i < urls.Length; i++)
        {
            if (VideoUrlParser.TryParse(urls[i], VideoProviders.All, out _))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Full Markdown rewrite of a page containing fenced code, inline prose links, and 12 multi-provider video embeds into a pre-sized buffer.</summary>
    /// <returns>Total UTF-8 bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int RewriteMultiProviderPage()
    {
        _reusableWriter.ResetWrittenCount();
        VideoRewriter.Rewrite(_multiProviderPage, _reusableWriter, VideoOptions.Default, _twitchParent);
        return _reusableWriter.WrittenCount;
    }

    /// <summary>Repeats <paramref name="chunk"/> <paramref name="times"/> times into a contiguous UTF-8 byte array.</summary>
    /// <param name="chunk">UTF-8 chunk bytes.</param>
    /// <param name="times">Repetition count.</param>
    /// <returns>Concatenated byte array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] Repeat(ReadOnlySpan<byte> chunk, int times)
    {
        var dst = new byte[chunk.Length * times];
        var span = dst.AsSpan();
        for (var i = 0; i < times; i++)
        {
            chunk.CopyTo(span[(i * chunk.Length)..]);
        }

        return dst;
    }

    /// <summary>Creates the representative provider URL array used by <see cref="ParseAllProviders"/>.</summary>
    /// <returns>Array of UTF-8 video URLs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[][] CreateProviderUrls() =>
    [
        [.. "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=1h2m3s"u8],
        [.. "https://youtu.be/dQw4w9WgXcQ"u8],
        [.. "https://vimeo.com/76979871/8f7e6d5c4b"u8],
        [.. "https://onedrive.live.com/embed?resid=12345!678&authkey=!AbCdEf"u8],
        [.. "https://web.microsoftstream.com/video/f6e5d4c3-b2a1-0000-1111-222233334444"u8],
        [.. "https://contoso.sharepoint.com/:v:/s/team/EaBcDeFg12345"u8],
        [.. "https://www.videoindexer.ai/accounts/00000000-0000-0000-0000-000000000000/videos/vid12345"u8],
        [.. "https://myaccount.blob.core.windows.net/videos/intro.mp4?sv=2025-01-01"u8],
        [.. "https://www.loom.com/share/8a9b0c1d2e3f4a5b"u8],
        [.. "https://www.dailymotion.com/video/x84sh87_launch-keynote"u8],
        [.. "https://www.twitch.tv/videos/1987654321"u8],
        [.. "https://reactiveui.wistia.com/medias/wistia987abc"u8],
        [.. "https://customer-f33zs165nr7gyfy4.cloudflarestream.com/6b9e68b07dfee8cc2d116e4c51d6a957/watch"u8],
        [.. "https://drive.google.com/file/d/1aBcDeFgHiJkLmNoPqRsTuVwXyZ/view"u8],
        [.. "https://www.bilibili.com/video/BV1xx411c7mD?p=2"u8],
        [.. "https://share.vidyard.com/watch/AbCdEfGh12345678"u8]
    ];
}
