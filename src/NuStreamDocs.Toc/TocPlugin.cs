// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Toc.Logging;

namespace NuStreamDocs.Toc;

/// <summary>
/// Per-page table-of-contents and permalink-anchor plugin, mirroring
/// the mkdocs <c>toc</c> markdown extension's runtime behavior.
/// </summary>
/// <remarks>
/// During the post-render phase:
/// <list type="number">
/// <item><description>Scan the input HTML for headings, slugify them.</description></item>
/// <item><description>Rewrite the body with id attributes + permalink anchors.</description></item>
/// <item><description>If <see cref="TocOptions.MarkerSubstitute"/> is true and a
/// <c>&lt;!--@@toc@@--&gt;</c> marker is present in the rewritten output,
/// splice the rendered TOC fragment in.</description></item>
/// </list>
/// </remarks>
public sealed class TocPlugin : IPagePostRenderPlugin
{
    /// <summary>Tiebreak that orders TOC marker substitution after the theme shell wrap (which uses the bare <see cref="PluginBand.Latest"/>).</summary>
    private const int PostRenderTiebreak = 1;

    /// <summary>Configured option set.</summary>
    private readonly TocOptions _options;

    /// <summary>Logger.</summary>
    private readonly ILogger _logger;

    /// <summary>UTF-8 bytes of <see cref="TocOptions.PermalinkSymbol"/> — encoded once at construction so the per-page rewrite never re-encodes the glyph.</summary>
    private readonly byte[] _permalinkSymbolBytes;

    /// <summary>Initializes a new instance of the <see cref="TocPlugin"/> class with default options.</summary>
    public TocPlugin()
        : this(TocOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TocPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public TocPlugin(in TocOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TocPlugin"/> class with a logger.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger.</param>
    public TocPlugin(in TocOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
        _permalinkSymbolBytes = string.IsNullOrEmpty(options.PermalinkSymbol)
            ? []
            : Encoding.UTF8.GetBytes(options.PermalinkSymbol);
    }

    /// <summary>Gets the UTF-8 marker bytes the theme places where the rendered TOC should land.</summary>
    public static byte[] TocMarker { get; } = [.. "<!--@@toc@@-->"u8];

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "toc"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, PostRenderTiebreak);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => !html.IsEmpty && html.IndexOf("<h"u8) >= 0;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        var snapshot = context.Html;
        var output = context.Output;
        if (snapshot.IsEmpty)
        {
            return;
        }

        TocLoggingHelper.LogTocStart(_logger, context.RelativePath.Value);
        var sw = Stopwatch.StartNew();

        var headings = HeadingScanner.Scan(snapshot);
        if (headings.Length is 0)
        {
            // No headings — pass through verbatim.
            Utf8StringWriter.Write(output, snapshot);
            sw.Stop();
            TocLoggingHelper.LogTocComplete(_logger, context.RelativePath.Value, 0, 0, sw.ElapsedMilliseconds);
            return;
        }

        var (slugged, collisions) = HeadingSlugifier.AssignSlugs(snapshot, headings);

        if (_options.MarkerSubstitute)
        {
            using var rental = PageBuilderPool.Rent(snapshot.Length);
            var scratch = rental.Writer;
            HeadingRewriter.Rewrite(snapshot, slugged, _permalinkSymbolBytes, scratch);
            SubstituteMarker(scratch.WrittenSpan, snapshot, slugged, output);
        }
        else
        {
            HeadingRewriter.Rewrite(snapshot, slugged, _permalinkSymbolBytes, output);
        }

        sw.Stop();
        TocLoggingHelper.LogTocComplete(_logger, context.RelativePath.Value, slugged.Length, collisions, sw.ElapsedMilliseconds);
    }

    /// <summary>Locates the TOC marker in the heading-rewritten body and either splices the fragment in or copies the body verbatim.</summary>
    /// <param name="rewritten">Heading-rewritten HTML produced for this page.</param>
    /// <param name="sourceSnapshot">Original pre-rewrite HTML snapshot used for heading-text slices.</param>
    /// <param name="headings">Slugged headings.</param>
    /// <param name="output">Final output sink.</param>
    private void SubstituteMarker(ReadOnlySpan<byte> rewritten, ReadOnlySpan<byte> sourceSnapshot, Heading[] headings, IBufferWriter<byte> output)
    {
        var markerIndex = rewritten.IndexOf(TocMarker.AsSpan());
        if (markerIndex < 0)
        {
            Utf8StringWriter.Write(output, rewritten);
            return;
        }

        var prefix = rewritten[..markerIndex];
        var suffix = rewritten[(markerIndex + TocMarker.Length)..];

        Utf8StringWriter.Write(output, prefix);
        TocFragmentRenderer.Render(sourceSnapshot, headings, in _options, output);
        Utf8StringWriter.Write(output, suffix);
    }
}
