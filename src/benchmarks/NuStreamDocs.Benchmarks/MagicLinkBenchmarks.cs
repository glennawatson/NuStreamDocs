// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.MagicLink;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the magic-link rewriter.</summary>
/// <remarks>
/// Three input fixtures cover the rewriter's three modes: a URL-heavy
/// paragraph (URL autolinking only), a release-notes block dense with
/// <c>#NNN</c> issue refs, and a collaborator-credits block dense with
/// <c>@user</c> mentions. Each is run twice — once with shortref
/// expansion off (matches the default plugin shape) and once on (the
/// rxui website configuration).
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class MagicLinkBenchmarks
{
    /// <summary>Number of times each fixture line is repeated to give the rewriter enough work to time.</summary>
    private const int Repetitions = 30;

    /// <summary>Headroom factor for the rewritten output (each shortref expands ~7×; 8× covers worst case).</summary>
    private const int OutputExpansionFactor = 8;

    /// <summary>Default test repo bytes used by the issue-ref expansion paths.</summary>
    private static readonly byte[] DefaultRepo = "reactiveui/ReactiveUI"u8.ToArray();

    /// <summary>Pre-built URL-heavy fixture.</summary>
    private byte[] _urlsFixture = [];

    /// <summary>Pre-built issue-ref-heavy fixture.</summary>
    private byte[] _issueRefsFixture = [];

    /// <summary>Pre-built mention-heavy fixture.</summary>
    private byte[] _mentionsFixture = [];

    /// <summary>Pre-built combined fixture (URLs + issue refs + mentions interleaved).</summary>
    private byte[] _combinedFixture = [];

    /// <summary>Generates the per-mode fixtures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _urlsFixture = Repeat(
            "Visit https://reactiveui.net for docs and https://github.com/reactiveui/ReactiveUI for source. Email mailto:rx@example.test if curious.\n"u8,
            Repetitions);

        _issueRefsFixture = Repeat(
            "Fixes #377, #382, #198, #1024 (also relates to #2048). See PR #500 plus follow-up #501 and #502.\n"u8,
            Repetitions);

        _mentionsFixture = Repeat(
            "Thanks @oliverw, @terenced, @2asoft, @chrisway, @vevix for the patches; cc @glennawatson and @reactivemarbles too.\n"u8,
            Repetitions);

        _combinedFixture = Repeat(
            "Release notes (#377): adds streaming support thanks @oliverw. See https://reactiveui.net/release/v20 for the upgrade guide; PR #382 lands #383.\n"u8,
            Repetitions);
    }

    /// <summary>URL autolinking only, shortref expansion disabled.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark(Baseline = true)]
    public int UrlsOnly() => Run(_urlsFixture, defaultRepo: [], expandMentions: false);

    /// <summary>Issue-ref expansion against a configured repo.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int IssueRefsExpanded() => Run(_issueRefsFixture, defaultRepo: DefaultRepo, expandMentions: false);

    /// <summary>Mention expansion only.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int MentionsExpanded() => Run(_mentionsFixture, defaultRepo: [], expandMentions: true);

    /// <summary>Combined URL autolinking + issue-ref + mention expansion (rxui release-notes shape).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int CombinedRxuiShape() => Run(_combinedFixture, defaultRepo: DefaultRepo, expandMentions: true);

    /// <summary>Repeats <paramref name="line"/> <paramref name="times"/> times into a single byte array.</summary>
    /// <param name="line">Source line bytes.</param>
    /// <param name="times">Repetition count.</param>
    /// <returns>The concatenated byte array.</returns>
    private static byte[] Repeat(ReadOnlySpan<byte> line, int times)
    {
        var dst = new byte[line.Length * times];
        var span = dst.AsSpan();
        for (var i = 0; i < times; i++)
        {
            line.CopyTo(span[(i * line.Length)..]);
        }

        return dst;
    }

    /// <summary>Drives a single rewrite into a sized writer and returns the byte count written.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="defaultRepo">Repo path bytes for issue-ref expansion (empty disables that pass).</param>
    /// <param name="expandMentions">Whether to expand <c>@user</c> mentions.</param>
    /// <returns>Bytes written.</returns>
    private static int Run(byte[] source, ReadOnlySpan<byte> defaultRepo, bool expandMentions)
    {
        ArrayBufferWriter<byte> writer = new(source.Length * OutputExpansionFactor);
        MagicLinkRewriter.Rewrite(source, writer, defaultRepo, expandMentions);
        return writer.WrittenCount;
    }
}
