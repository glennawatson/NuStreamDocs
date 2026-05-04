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
/// During <see cref="OnRenderPageAsync"/>:
/// <list type="number">
/// <item><description>Snapshot the page bytes into a pooled buffer.</description></item>
/// <item><description>Scan for headings, slugify, and rewrite the body with id attributes + permalink anchors.</description></item>
/// <item><description>If <see cref="TocOptions.MarkerSubstitute"/> is true and a
/// <c>&lt;!--@@toc@@--&gt;</c> marker is present, take a second snapshot and
/// splice the rendered TOC fragment in.</description></item>
/// </list>
/// </remarks>
public sealed class TocPlugin : IDocPlugin
{
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
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var html = context.Html;
        var written = html.WrittenSpan;
        if (written.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        TocLoggingHelper.LogTocStart(_logger, context.RelativePath.Value);
        var sw = Stopwatch.StartNew();

        var length = written.Length;
        var rental = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            written.CopyTo(rental);
            var snapshot = rental.AsSpan(0, length);

            var headings = HeadingScanner.Scan(snapshot);
            if (headings.Length is 0)
            {
                sw.Stop();
                TocLoggingHelper.LogTocComplete(_logger, context.RelativePath.Value, 0, 0, sw.ElapsedMilliseconds);
                return ValueTask.CompletedTask;
            }

            var (slugged, collisions) = HeadingSlugifier.AssignSlugs(snapshot, headings);

            html.ResetWrittenCount();
            HeadingRewriter.Rewrite(snapshot, slugged, _permalinkSymbolBytes, html);

            if (_options.MarkerSubstitute)
            {
                SubstituteMarker(html, snapshot, slugged);
            }

            sw.Stop();
            TocLoggingHelper.LogTocComplete(_logger, context.RelativePath.Value, slugged.Length, collisions, sw.ElapsedMilliseconds);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rental);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <summary>Locates the TOC marker in the freshly-rewritten body and substitutes the fragment.</summary>
    /// <param name="html">Output buffer (already carries the heading-rewritten body).</param>
    /// <param name="sourceSnapshot">Original pre-rewrite HTML snapshot used for heading-text slices.</param>
    /// <param name="headings">Slugged headings.</param>
    private void SubstituteMarker(ArrayBufferWriter<byte> html, ReadOnlySpan<byte> sourceSnapshot, Heading[] headings)
    {
        var written = html.WrittenSpan;
        var markerIndex = written.IndexOf(TocMarker.AsSpan());
        if (markerIndex < 0)
        {
            return;
        }

        var length = written.Length;
        var rental = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            written.CopyTo(rental);
            html.ResetWrittenCount();

            var snapshot = rental.AsSpan(0, length);
            var prefix = snapshot[..markerIndex];
            var suffix = snapshot[(markerIndex + TocMarker.Length)..];

            Utf8StringWriter.Write(html, prefix);
            TocFragmentRenderer.Render(sourceSnapshot, headings, in _options, html);
            Utf8StringWriter.Write(html, suffix);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rental);
        }
    }
}
