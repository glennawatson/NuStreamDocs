// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
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
    /// <summary>UTF-8 marker themes embed where the rendered TOC should land.</summary>
    private static readonly byte[] MarkerBytes = [.. "<!--@@toc@@-->"u8];

    /// <summary>Configured option set.</summary>
    private readonly TocOptions _options;

    /// <summary>Logger.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="TocPlugin"/> class with default options.</summary>
    public TocPlugin()
        : this(TocOptions.Default, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TocPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public TocPlugin(in TocOptions options)
        : this(options, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
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
    }

    /// <summary>Gets the marker the theme places where the rendered TOC should land.</summary>
    public static string TocMarker => "<!--@@toc@@-->";

    /// <inheritdoc/>
    public string Name => "toc";

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

        TocLoggingHelper.LogTocStart(_logger, context.RelativePath);
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
                TocLoggingHelper.LogTocComplete(_logger, context.RelativePath, 0, 0, sw.ElapsedMilliseconds);
                return ValueTask.CompletedTask;
            }

            var (slugged, collisions) = HeadingSlugifier.AssignSlugs(snapshot, headings);

            html.ResetWrittenCount();
            HeadingRewriter.Rewrite(snapshot, slugged, _options.PermalinkSymbol, html);

            if (_options.MarkerSubstitute)
            {
                SubstituteMarker(html, slugged);
            }

            sw.Stop();
            TocLoggingHelper.LogTocComplete(_logger, context.RelativePath, slugged.Length, collisions, sw.ElapsedMilliseconds);
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
    /// <param name="headings">Slugged headings.</param>
    private void SubstituteMarker(ArrayBufferWriter<byte> html, Heading[] headings)
    {
        var written = html.WrittenSpan;
        var markerIndex = written.IndexOf(MarkerBytes.AsSpan());
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
            var suffix = snapshot[(markerIndex + MarkerBytes.Length)..];

            Utf8StringWriter.Write(html, prefix);
            TocFragmentRenderer.Render(snapshot, headings, in _options, html);
            Utf8StringWriter.Write(html, suffix);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rental);
        }
    }
}
