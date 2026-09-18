// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Tags;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Baseline benchmark for <see cref="TagsPlugin"/>'s discover-phase synthesis.
/// Already on the pooled <c>PageBuilderPool</c> — captured for reference so any
/// future change to the synthesis path can be compared back.
/// </summary>
[DebuggerDisplay("TagsPluginBenchmarks: Pages={Pages}")]
[ShortRunJob]
[MemoryDiagnoser]
public class TagsPluginBenchmarks
{
    /// <summary>Reserves enough text space for the generated page fixture.</summary>
    private const int PostTextCapacity = 512;

    /// <summary>Small page count (smoke).</summary>
    private const int SmallPages = 20;

    /// <summary>Medium page count (typical project).</summary>
    private const int MediumPages = 200;

    /// <summary>Large page count (stress).</summary>
    private const int LargePages = 1000;

    /// <summary>Distinct tag values cycled across the synthetic pages.</summary>
    private static readonly string[] Tags =
    [
        "guide",
        "tutorial",
        "reference",
        "concept",
        "release",
        "deprecated"
    ];

    /// <summary>Absolute path to the synthetic docs root.</summary>
    private string _docsRoot = string.Empty;

    /// <summary>Pre-built plugin instance reused per iteration.</summary>
    private TagsPlugin _plugin = null!;

    /// <summary>Gets or sets the page count for the current parameter set.</summary>
    [Params(SmallPages, MediumPages, LargePages)]
    public int Pages { get; set; }

    /// <summary>Generates the synthetic tagged-page corpus once per parameter set.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _docsRoot = Path.Combine(
            Path.GetTempPath(),
            $"smkd-bench-tags-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        _ = Directory.CreateDirectory(_docsRoot);

        for (var i = 0; i < Pages; i++)
        {
            var tag = Tags[i % Tags.Length];
            File.WriteAllText(
                Path.Combine(_docsRoot, $"page-{i.ToString(CultureInfo.InvariantCulture)}.md"),
                BuildPostBody(i, tag));
        }

        _plugin = new();
    }

    /// <summary>Cleans the corpus once at the end.</summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        try
        {
            if (Directory.Exists(_docsRoot))
            {
                Directory.Delete(_docsRoot, true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>Runs <see cref="TagsPlugin.DiscoverAsync"/> against the corpus, registering pages with a fresh per-iteration <see cref="SyntheticPageSink"/>.</summary>
    /// <returns>Number of synthetic pages registered (returned so BenchmarkDotNet doesn't elide the call).</returns>
    [Benchmark]
    public async ValueTask<int> Discover()
    {
        SyntheticPageSink sink = new();
        BuildDiscoverContext context = new((DirectoryPath)_docsRoot, (DirectoryPath)"/out", [], sink);
        await _plugin.DiscoverAsync(context, CancellationToken.None).ConfigureAwait(false);
        return sink.Count;
    }

    /// <summary>Builds a realistic-shape post body with frontmatter (title/tag) and a few paragraphs of prose.</summary>
    /// <param name="index">Post index.</param>
    /// <param name="tag">Tag the post belongs to.</param>
    /// <returns>Markdown source.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ApiCompatString BuildPostBody(int index, ApiCompatString tag) =>
        new StringBuilder(PostTextCapacity)
            .Append("---\n")
            .Append("tags:\n  - ").Append(tag.Value).Append('\n')
            .Append("---\n\n")
            .Append("# Page ").Append(index.ToString(CultureInfo.InvariantCulture)).Append("\n\n")
            .Append("First paragraph for page ").Append(index.ToString(CultureInfo.InvariantCulture)).Append(".\n\n")
            .Append("Second paragraph with a `code` reference.\n")
            .ToString();
}
