// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>Coverage tests for <see cref="PagefindSearchPlugin"/>.</summary>
/// <remarks>
/// End-to-end CLI invocation lives in <c>PagefindCliIntegrationTests</c> — that's where the
/// real-Pagefind smoke runs against a hand-rolled HTML fixture. These tests cover the
/// scan/finalize/head-extra plumbing exclusively.
/// </remarks>
public class PagefindSearchPluginTests
{
    /// <summary>Empty Scan input is skipped without adding a document.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyHtmlSkipsDocument()
    {
        PagefindSearchPlugin plugin = new();
        ScanPage(plugin, "page.md", default, default);
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }

    /// <summary>HTML without a heading falls back to the filename stem as the title.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoHeadingFallsBackToFilenameStem()
    {
        PagefindSearchPlugin plugin = new();
        ScanPage(plugin, "guide/intro.md", default, "<p>just text</p>"u8);
        var docs = plugin.DocumentsSnapshot();
        await Assert.That(docs.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(docs[0].Title)).IsEqualTo("intro");
    }

    /// <summary>SearchableFrontmatterKeys appends extracted bytes into the document text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SearchableFrontmatterKeysAppendBytes()
    {
        PagefindSearchPlugin plugin = new(PagefindOptions.Default with { SearchableFrontmatterKeys = [[.. "tags"u8]] });
        ScanPage(plugin, "page.md", "---\ntags: [foo, bar]\n---\nbody"u8, "<h1>Hi</h1><p>body</p>"u8);
        var docs = plugin.DocumentsSnapshot();
        await Assert.That(docs.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(docs[0].Text)).Contains("foo");
    }

    /// <summary>FinalizeAsync with an empty output root is a silent no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FinalizeAsyncEmptyRootNoOp()
    {
        PagefindSearchPlugin plugin = new();
        await plugin.FinalizeAsync(new(string.Empty, []), CancellationToken.None);
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }

    /// <summary>
    /// WriteHeadExtra omits the universal <c>nustreamdocs:search-index</c> meta tag — Pagefind
    /// doesn't ship a JSON manifest the theme should fetch — and instead injects the loader +
    /// bind-glue script tags.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraEmitsLoaderAndBindScript()
    {
        PagefindSearchPlugin plugin = new();
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        var rendered = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(rendered).DoesNotContain("nustreamdocs:search-index");
        await Assert.That(rendered).Contains("/pagefind/pagefind.js");
        await Assert.That(rendered).Contains("/assets/javascripts/pagefind-bind.js");
    }

    /// <summary>Name returns "search".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        PagefindSearchPlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("search"u8)).IsTrue();
    }

    /// <summary>Drives one Scan call against the plugin.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="relativePath">Source-relative markdown path.</param>
    /// <param name="source">Markdown bytes (frontmatter + body).</param>
    /// <param name="html">Rendered HTML bytes.</param>
    private static void ScanPage(
        PagefindSearchPlugin plugin,
        string relativePath,
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> html)
    {
        PageScanContext ctx = new(relativePath, source, html);
        plugin.Scan(in ctx);
    }
}
