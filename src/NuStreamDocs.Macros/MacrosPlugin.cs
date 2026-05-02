// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Macros.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Macros;

/// <summary>
/// Variable-substitution preprocessor for NuStreamDocs markdown. Reads
/// <c>{{ name }}</c> markers from the source, looks each name up in the
/// host-supplied <see cref="MacrosOptions.Variables"/> map, and emits
/// the resolved value into the markdown stream before the parser runs.
/// Closes the mkdocs-macros gap for the variable-substitution use case.
/// </summary>
public sealed class MacrosPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <summary>Configured options.</summary>
    private readonly MacrosOptions _options;

    /// <summary>Logger used for missing-variable warnings.</summary>
    private readonly ILogger _logger;

    /// <summary>Cached lookup delegate so each preprocess call doesn't allocate a fresh closure.</summary>
    private readonly MacrosScanner.Lookup _lookup;

    /// <summary>Cached missing-name callback (or null when warnings are off).</summary>
    private readonly MacrosScanner.MissingCallback? _onMissing;

    /// <summary>Initializes a new instance of the <see cref="MacrosPlugin"/> class with default options.</summary>
    public MacrosPlugin()
        : this(MacrosOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MacrosPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public MacrosPlugin(MacrosOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MacrosPlugin"/> class with a logger.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger.</param>
    public MacrosPlugin(MacrosOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
        _lookup = ResolveVariable;
        _onMissing = options.WarnOnMissing ? WarnMissing : null;
    }

    /// <inheritdoc/>
    public override string Name => "macros";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (_options.Variables.Count is 0 || source.IndexOf("{{"u8) < 0)
        {
            CopyThrough(source, writer);
            return;
        }

        MacrosScanner.Rewrite(source, _lookup, _options.EscapeHtml, _onMissing, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, string relativePath) =>
        Preprocess(source, writer);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <summary>Copies <paramref name="source"/> through to <paramref name="writer"/> without scanning.</summary>
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

    /// <summary>Lookup callback bound to the configured <see cref="MacrosOptions.Variables"/> dictionary.</summary>
    /// <param name="name">UTF-8 name bytes.</param>
    /// <param name="value">Resolved value on hit.</param>
    /// <returns>True when the name is in the dictionary.</returns>
    private bool ResolveVariable(ReadOnlySpan<byte> name, out string value)
    {
        var key = Encoding.UTF8.GetString(name);
        return _options.Variables.TryGetValue(key, out value!);
    }

    /// <summary>Missing-name callback that logs at <c>Warning</c> via the source-generated helper.</summary>
    /// <param name="name">UTF-8 name bytes.</param>
    private void WarnMissing(ReadOnlySpan<byte> name)
    {
        var key = Encoding.UTF8.GetString(name);
        MacrosLoggingHelper.LogMissingVariable(_logger, key);
    }
}
