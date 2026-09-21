// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using NuStreamDocs.Building;
using NuStreamDocs.MarkdownExtensions.AttrList;
using NuStreamDocs.Mermaid;

namespace NuStreamDocs.Benchmarks;

/// <summary>Allocation profile of <c>DocBuilder.RenderPageAsync</c>: the Markdown render followed by the post-render plugin rotation over pooled page buffers.</summary>
/// <remarks>
/// Every benchmark renders a pre-built UTF-8 page into a reused writer, so the sampled allocations belong to the
/// per-page path. The variants differ in how many post-render plugins rewrite the page: none registered, none
/// needing a rewrite, one rewrite (the final HTML lives in the scratch buffer), and two rewrites (the final HTML
/// lives in the input buffer).
/// </remarks>
[DebuggerDisplay("DocBuilderPageRenderBenchmarks: writer={_writer}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class DocBuilderPageRenderBenchmarks
{
    /// <summary>Approximate size of each page, in bytes.</summary>
    private const int TargetPageBytes = 3072;

    /// <summary>Initial capacity of the reused output writer.</summary>
    private const int OutputCapacity = 64 * 1024;

    /// <summary>Path of the rendered page.</summary>
    private const string PagePath = "guide/intro.md";

    /// <summary>Headings, lists, inline emphasis and fenced code with no attr-list or mermaid content.</summary>
    private const string RichTemplate =
        """
        ## Section @

        A paragraph with **bold**, *emphasis*, `code` and a [link](https://example.com/a@ "Title").

        1. First step
        2. Second step with `code`
        3. Third step

        - Bullet alpha
        - Bullet beta
            - nested bullet

        ```csharp
        var x = @ < 2 && y > 3;
        Console.WriteLine(x);
        ```

        """;

    /// <summary>Rich content where every heading carries an attr-list block.</summary>
    private const string AttrListTemplate =
        """
        ## Section @ {: #section-@ .lead }

        A paragraph with **bold**, *emphasis*, `code` and a [link](https://example.com/a@ "Title").

        - Bullet alpha
        - Bullet beta

        1. First step
        2. Second step

        """;

    /// <summary>Rich content with attr-list headings and a mermaid fence in every repetition.</summary>
    private const string AttrListAndMermaidTemplate =
        """
        ## Section @ {: #section-@ .lead }

        A paragraph with **bold**, *emphasis* and a [link](https://example.com/a@ "Title").

        ```mermaid
        graph TD; A@-->B;
        ```

        """;

    /// <summary>Plain sentences that hold no Markdown syntax.</summary>
    private const string PlainTemplate = "A plain sentence number @ with only words and spaces and nothing that needs any rendering at all\n\n";

    /// <summary>Reused output writer, reset per invocation so sampled bytes belong to the per-page path.</summary>
    private ArrayBufferWriter<byte> _writer = null!;

    /// <summary>Builder with no plugins.</summary>
    private DocBuilder _noPlugins = null!;

    /// <summary>Builder with attr-list and mermaid post-render plugins.</summary>
    private DocBuilder _postRenderPlugins = null!;

    /// <summary>Page with headings, lists and code.</summary>
    private byte[] _richPage = [];

    /// <summary>Page with attr-list headings.</summary>
    private byte[] _attrListPage = [];

    /// <summary>Page with attr-list headings and mermaid fences.</summary>
    private byte[] _attrListAndMermaidPage = [];

    /// <summary>Page without Markdown syntax.</summary>
    private byte[] _plainPage = [];

    /// <summary>Builds the builders and every input page once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _writer = new(OutputCapacity);
        _noPlugins = new();
        _postRenderPlugins = new DocBuilder()
            .UsePlugin(new AttrListPlugin())
            .UsePlugin(new MermaidPlugin());

        _richPage = Encoding.UTF8.GetBytes(Repeat(RichTemplate));
        _attrListPage = Encoding.UTF8.GetBytes(Repeat(AttrListTemplate));
        _attrListAndMermaidPage = Encoding.UTF8.GetBytes(Repeat(AttrListAndMermaidTemplate));
        _plainPage = Encoding.UTF8.GetBytes(Repeat(PlainTemplate));
    }

    /// <summary>Renders a rich page with no post-render plugin registered.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> RichPageWithoutPlugins() => RenderInto(_noPlugins, _richPage);

    /// <summary>Renders a rich page through plugins that all decline to rewrite it.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> RichPageNoPluginRewrites() => RenderInto(_postRenderPlugins, _richPage);

    /// <summary>Renders a page that one plugin rewrites, leaving the final HTML in the scratch buffer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> OneRewritingPlugin() => RenderInto(_postRenderPlugins, _attrListPage);

    /// <summary>Renders a page that both plugins rewrite, leaving the final HTML in the input buffer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> TwoRewritingPlugins() => RenderInto(_postRenderPlugins, _attrListAndMermaidPage);

    /// <summary>Renders a page without Markdown syntax through plugins that decline to rewrite it.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> PlainPageNoPluginRewrites() => RenderInto(_postRenderPlugins, _plainPage);

    /// <summary>Repeats <paramref name="template"/> until the page reaches the target size, replacing <c>@</c> with the repetition index.</summary>
    /// <param name="template">Page fragment; <c>@</c> marks the index slot.</param>
    /// <returns>The assembled page.</returns>
    private static string Repeat(string template)
    {
        StringBuilder sb = new(TargetPageBytes + template.Length);
        for (var i = 0; sb.Length < TargetPageBytes; i++)
        {
            _ = sb.Append(template.Replace("@", i.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
        }

        return sb.ToString();
    }

    /// <summary>Renders <paramref name="source"/> through <paramref name="builder"/> into the reused writer.</summary>
    /// <param name="builder">Builder whose plugins run after the render.</param>
    /// <param name="source">UTF-8 markdown.</param>
    /// <returns>Bytes written.</returns>
    private async ValueTask<int> RenderInto(DocBuilder builder, byte[] source)
    {
        _writer.ResetWrittenCount();
        await builder.RenderPageAsync(PagePath, source, _writer, CancellationToken.None).ConfigureAwait(false);
        return _writer.WrittenCount;
    }
}
