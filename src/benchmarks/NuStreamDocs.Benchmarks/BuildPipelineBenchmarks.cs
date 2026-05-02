// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Building;
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
[ShortRunJob]
[MemoryDiagnoser]
[SuppressMessage(
    "Major Code Smell",
    "S4462:Calls to \"async\" methods should not be blocking",
    Justification = "BenchmarkDotNet drives benchmarks synchronously; GetResult is the pragmatic way to measure end-to-end async pipelines.")]
public class BuildPipelineBenchmarks
{
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
        _inputRoot = Path.Combine(Path.GetTempPath(), "smkd-bench-in-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        _outputRoot = Path.Combine(Path.GetTempPath(), "smkd-bench-out-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_inputRoot);

        // Spread pages across nested sections so nav rendering exercises depth.
        for (var i = 0; i < Pages; i++)
        {
            var bucket = i % 5;
            var dir = bucket switch
            {
                0 => _inputRoot,
                1 => Path.Combine(_inputRoot, "guide"),
                2 => Path.Combine(_inputRoot, "guide", "deep"),
                3 => Path.Combine(_inputRoot, "reference"),
                _ => Path.Combine(_inputRoot, "blog"),
            };
            Directory.CreateDirectory(dir);
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
    [IterationSetup]
    public void IterationSetup() => ResetOutput();

    /// <summary>Build pipeline with no plugins (pure render + write).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark(Baseline = true)]
    public int Baseline() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with the markdown-extension bundle.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int WithMarkdownExtensions() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseCommonMarkdownExtensions()
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with syntax highlighting.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int WithHighlight() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseHighlight()
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with the nav plugin (full render).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int WithNav() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseNav()
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with privacy in audit-only mode (no network).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int WithPrivacyAuditOnly() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UsePrivacy(static opts => opts with { AuditOnly = true })
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with mermaid retag (no diagrams in fixture, so it's a pre-filter cost only).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int WithMermaid() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseMermaid()
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with all the in-process plugins stacked: markdown extensions + highlight + nav + mermaid + privacy audit.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int FullStackInProcess() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .UseCommonMarkdownExtensions()
            .UseHighlight()
            .UseNav()
            .UseMermaid()
            .UsePrivacy(static opts => opts with { AuditOnly = true })
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Generates one realistic markdown page.</summary>
    /// <param name="index">Page index for unique anchors.</param>
    /// <returns>Markdown source.</returns>
    private static string Page(int index) =>
        new StringBuilder(1024)
            .Append("# Page ").Append(index).Append('\n').Append('\n')
            .Append("Some intro text with **bold** and `code` and a [link](https://example.com/").Append(index).Append(").\n\n")
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
                Directory.Delete(path, recursive: true);
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
        Directory.CreateDirectory(_outputRoot);
    }
}
