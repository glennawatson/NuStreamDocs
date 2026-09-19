// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Autorefs;
using NuStreamDocs.Bibliography;
using NuStreamDocs.Building;
using NuStreamDocs.Highlight;
using NuStreamDocs.Icons.MaterialDesign;
using NuStreamDocs.Macros;
using NuStreamDocs.MagicLink;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Mermaid;
using NuStreamDocs.Nav;
using NuStreamDocs.Search.Lunr;
using NuStreamDocs.Snippets;
using NuStreamDocs.SphinxInventory;
using NuStreamDocs.Theme.Material.IconShortcode;

namespace NuStreamDocs.Benchmarks;

/// <summary>Measures documentation builds against the local ReactiveUI website corpus.</summary>
[DebuggerDisplay("RxuiCorpusBenchmarks: outputRoot={_outputRoot}")]
[ShortRunJob]
[MemoryDiagnoser]
public class RxuiCorpusBenchmarks
{
    /// <summary>Absolute path to the maintainer's local rxui-website corpus checkout.</summary>
    private const string RxuiDocsRoot = "/home/glennw/source/rxui/website/docs";

    /// <summary>Approximate heading-id count for the rxui corpus (~13.8K pages × ~10 headings each).</summary>
    /// <remarks>
    /// Pre-sizes the autorefs registry so its <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>
    /// lands at the right bucket count immediately rather than doubling
    /// through 4, 8, 16, … 131072. The micro-bench at 100K entries
    /// shows this halves registry allocation.
    /// </remarks>
    private const int RxuiHeadingHint = 150_000;

    /// <summary>Per-iteration output directory created under the system temp.</summary>
    private string _outputRoot = string.Empty;

    /// <summary>Allocates a fresh per-iteration output directory.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        TryDelete(_outputRoot);
        _outputRoot = Path.Combine(
            Path.GetTempPath(),
            $"smkd-rxui-out-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        _ = Directory.CreateDirectory(_outputRoot);
    }

    /// <summary>Cleans the last iteration's output directory.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [GlobalCleanup]
    public void GlobalCleanup() => TryDelete(_outputRoot);

    /// <summary>Baseline: pure render + write, no plugins.</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark(Baseline = true)]
    public Task<int> Baseline() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .BuildAsync();

    /// <summary>Build with the markdown-extension bundle (admonitions, details, tabs, footnotes, …).</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithMarkdownExtensions() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseCommonMarkdownExtensions()
            .BuildAsync();

    /// <summary>Build with syntax highlighting on every fenced block.</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithHighlight() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseHighlight()
            .BuildAsync();

    /// <summary>Build with nav generation (full discovery + per-page render of the active branch).</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithNav() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseNav()
            .BuildAsync();

    /// <summary>Build with magic-link URL autolinking + GitHub-shortref expansion against the rxui repo.</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithMagicLink() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseMagicLink(new() { DefaultRepo = "reactiveui/ReactiveUI"u8.ToArray(), ExpandUserMentions = true })
            .BuildAsync();

    /// <summary>Build with the full in-process plugin stack — markdown extensions + highlight + magic-link + nav + autorefs + search + mermaid.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public Task<int> FullStack()
    {
        AutorefsRegistry registry = new(RxuiHeadingHint);
        return new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseCommonMarkdownExtensions()
            .UseHighlight()
            .UseMagicLink(new() { DefaultRepo = "reactiveui/ReactiveUI"u8.ToArray(), ExpandUserMentions = true })
            .UseNav()
            .UseAutorefs(registry)
            .UseLunrSearch()
            .UseMermaid()
            .BuildAsync();
    }

    /// <summary>Build with snippet-include preprocessor (whole-file + section markers).</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithSnippets() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseSnippets()
            .BuildAsync();

    /// <summary>Build with the macros preprocessor (<c>{{ name }}</c> substitution).</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithMacros() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseMacros(static opts => opts.WithVariable("project", "ReactiveUI"))
            .BuildAsync();

    /// <summary>Build with the bibliography preprocessor — empty database, exercises the marker scanner only.</summary>
    /// <returns>Pages processed.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public Task<int> WithBibliography() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseBibliography(BibliographyOptions.Default)
            .BuildAsync();

    /// <summary>Build with the MDI inline-SVG resolver wired into the icon shortcode rewriter (~7400 entries).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public Task<int> WithMdiIcons()
    {
        MdiIconResolver resolver = new();
        return new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UsePlugin(new IconShortcodePlugin(resolver))
            .BuildAsync();
    }

    /// <summary>Build with the Sphinx-inventory finalize emitter (drives the autorefs registry snapshot path).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public Task<int> WithSphinxInventory()
    {
        AutorefsRegistry registry = new(RxuiHeadingHint);
        return new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseAutorefs(registry)
            .UseSphinxInventory(registry)
            .BuildAsync();
    }

    /// <summary>Builds with the combined snippet, macro, bibliography, Markdown, navigation, search, and icon stack.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public Task<int> EverythingStack()
    {
        AutorefsRegistry registry = new(RxuiHeadingHint);
        MdiIconResolver iconResolver = new();
        return new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseSnippets()
            .UseMacros(static opts => opts.WithVariable("project", "ReactiveUI"))
            .UseBibliography(BibliographyOptions.Default)
            .UseCommonMarkdownExtensions()
            .UseHighlight()
            .UseNav()
            .UseAutorefs(registry)
            .UseLunrSearch()
            .UseMermaid()
            .UseSphinxInventory(registry)
            .UsePlugin(new IconShortcodePlugin(iconResolver))
            .BuildAsync();
    }

    /// <summary>Best-effort recursive directory delete.</summary>
    /// <param name="path">Directory path.</param>
    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; benchmarks fail-soft on tear-down.
        }
    }
}
