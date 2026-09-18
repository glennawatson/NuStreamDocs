// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Highlight;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Mermaid;
using NuStreamDocs.Nav;
using NuStreamDocs.Privacy;

namespace NuStreamDocs.Benchmarks;

/// <summary>End-to-end <c>DocBuilder</c> benchmarks across plugin combinations, against a synthesized on-disk corpus.</summary>
/// <remarks>
/// The synthetic corpus is built once per parameter set under
/// <c>Path.GetTempPath</c> in <c>GlobalSetup</c> and torn
/// down in <c>GlobalCleanup</c>. Every benchmark invocation
/// runs a fresh <c>DocBuilder</c> against the same input root,
/// so individual times measure the per-build cost (parse + render +
/// plugin hooks + write) without the corpus-creation overhead.
/// </remarks>
[DebuggerDisplay("BuildPipelineBenchmarks: Pages={Pages}")]
[ShortRunJob]
[MemoryDiagnoser]
public class BuildPipelineBenchmarks
{
    /// <summary>Reserves enough text space for each generated page.</summary>
    private const int PageTextCapacity = 1024;

    /// <summary>Small synthetic-corpus size (smoke).</summary>
    private const int SmallPages = 50;

    /// <summary>Medium synthetic-corpus size (typical project).</summary>
    private const int MediumPages = 500;

    /// <summary>Absolute path to the corpus input root for the active <c>Pages</c> param.</summary>
    private string _inputRoot = string.Empty;

    /// <summary>Absolute path to a fresh per-iteration output directory.</summary>
    private string _outputRoot = string.Empty;

    /// <summary>Gets or sets the page count for the current parameter set.</summary>
    [Params(SmallPages, MediumPages)]
    public int Pages { get; set; }

    /// <summary>Generates the input corpus once per parameter set.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _inputRoot = Path.Combine(
            Path.GetTempPath(),
            $"smkd-bench-in-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        _outputRoot = Path.Combine(
            Path.GetTempPath(),
            $"smkd-bench-out-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        _ = Directory.CreateDirectory(_inputRoot);

        // Spread pages across nested sections so nav rendering exercises depth.
        DirectoryPath[] directories =
        [
            _inputRoot,
            Path.Combine(_inputRoot, "guide"),
            Path.Combine(_inputRoot, "guide", "deep"),
            Path.Combine(_inputRoot, "reference"),
            Path.Combine(_inputRoot, "blog")
        ];
        for (var i = 0; i < Pages; i++)
        {
            var dir = directories[i % directories.Length];
            _ = Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"page-{i}.md"), Page(i));
        }
    }

    /// <summary>Cleans up the corpus once at the end.</summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        TryDelete(_inputRoot);
        TryDelete(_outputRoot);
    }

    /// <summary>Resets the per-iteration output directory.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [IterationSetup]
    public void IterationSetup() => ResetOutput();

    /// <summary>Build pipeline with no plugins (pure render + write).</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark(Baseline = true)]
    public Task<int> Baseline() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .BuildAsync();

    /// <summary>Build with the markdown-extension bundle.</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithMarkdownExtensions() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseCommonMarkdownExtensions()
            .BuildAsync();

    /// <summary>Build with syntax highlighting.</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithHighlight() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseHighlight()
            .BuildAsync();

    /// <summary>Build with the nav plugin (full render).</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithNav() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseNav()
            .BuildAsync();

    /// <summary>Build with privacy in audit-only mode (no network).</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithPrivacyAuditOnly() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UsePrivacy(static opts => opts with { AuditOnly = true })
            .BuildAsync();

    /// <summary>Build with mermaid retag (no diagrams in fixture, so it's a pre-filter cost only).</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithMermaid() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseMermaid()
            .BuildAsync();

    /// <summary>Build with all the in-process plugins stacked: markdown extensions + highlight + nav + mermaid + privacy audit.</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> FullStackInProcess() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseCommonMarkdownExtensions()
            .UseHighlight()
            .UseNav()
            .UseMermaid()
            .UsePrivacy(static opts => opts with { AuditOnly = true })
            .BuildAsync();

    /// <summary>Generates one realistic markdown page.</summary>
    /// <param name="index">Page index for unique anchors.</param>
    /// <returns>Markdown source.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ApiCompatString Page(int index) =>
        new StringBuilder(PageTextCapacity)
            .Append("# Page ").Append(index).Append('\n').Append('\n')
            .Append("Some intro text with **bold** and `code` and a [link](https://example.com/").Append(index)
            .Append(").\n\n")
            .Append("## Code\n\n```csharp\npublic int Add(int a, int b) => a + b;\n```\n\n")
            .Append("!!! note \"Heads up\"\n    body line one\n    body line two\n\n")
            .Append("- [x] done item\n- [ ] todo item\n- regular item\n\n")
            .Append("| h1 | h2 |\n| --- | --- |\n| a | b |\n| c | d |\n\n")
            .Append("Term ").Append(index).Append("\n: A definition.\n\n")
            .Append("See[^").Append(index).Append("] for more.\n\n[^").Append(index).Append("]: a footnote.\n")
            .ToString();

    /// <summary>Best-effort recursive directory delete.</summary>
    /// <param name="path">Directory path.</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>Wipes and re-creates the per-iteration output directory.</summary>
    private void ResetOutput()
    {
        TryDelete(_outputRoot);
        _ = Directory.CreateDirectory(_outputRoot);
    }
}
