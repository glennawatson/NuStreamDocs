// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SmartSymbols;

/// <summary>
/// Smart-symbols plugin. Pre-processes raw Markdown so that the
/// pymdownx.smartsymbols default substitutions —
/// <c>(c)</c>, <c>(r)</c>, <c>(tm)</c>, <c>c/o</c>, <c>+/-</c>,
/// <c>=/=</c>, the four arrow forms, and the three common
/// fractions — render as their Unicode characters in the final
/// HTML.
/// </summary>
/// <remarks>
/// The rewriter walks the source as UTF-8 bytes and skips fenced-
/// code regions and inline-code spans verbatim, matching
/// pymdownx.smartsymbols' inline-only scope. This is the parity
/// list enabled by Zensical's default <c>zensical.toml</c>.
/// </remarks>
public sealed class SmartSymbolsPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Name => "smartsymbols"u8;

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        SmartSymbolsRewriter.Rewrite(source, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, FilePath relativePath) =>
        Preprocess(source, writer);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;
}
