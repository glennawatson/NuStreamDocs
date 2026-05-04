// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Bibliography.Logging;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Pre-render preprocessor that resolves pandoc-style citation markers
/// — <c>[@key]</c>, <c>[@key, p 23]</c>, <c>[@a; @b]</c> — into footnote
/// references and appends a Bibliography section to each page.
/// </summary>
public sealed class BibliographyPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <summary>Resolved options.</summary>
    private readonly BibliographyOptions _options;

    /// <summary>Logger used for missing-citation warnings.</summary>
    private readonly ILogger _logger;

    /// <summary>Cached missing-name callback (or null when warnings are off).</summary>
    private readonly MissingCitationCallback? _onMissing;

    /// <summary>Initializes a new instance of the <see cref="BibliographyPlugin"/> class with default options.</summary>
    public BibliographyPlugin()
        : this(BibliographyOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BibliographyPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public BibliographyPlugin(BibliographyOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BibliographyPlugin"/> class with a logger.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger.</param>
    public BibliographyPlugin(BibliographyOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
        _onMissing = options.WarnOnMissing ? WarnMissing : null;
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Name => "bibliography"u8;

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (_options.Database.Count is 0 || source.IndexOf("[@"u8) < 0)
        {
            CopyThrough(source, writer);
            return;
        }

        if (BibliographyRewriter.Rewrite(source, _options.Database, _options.Style, _onMissing, writer))
        {
            return;
        }

        CopyThrough(source, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, FilePath relativePath) =>
        Preprocess(source, writer);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <summary>Copies <paramref name="source"/> through to <paramref name="writer"/> without rewriting.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void CopyThrough(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        if (source.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(source.Length);
        source.CopyTo(dst);
        writer.Advance(source.Length);
    }

    /// <summary>Missing-citation callback that logs at <c>Warning</c>.</summary>
    /// <param name="key">The unresolved key.</param>
    private void WarnMissing(string key) =>
        BibliographyLoggingHelper.LogMissingCitation(_logger, key);
}
