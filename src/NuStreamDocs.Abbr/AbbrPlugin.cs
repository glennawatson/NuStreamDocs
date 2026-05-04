// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Abbr;

/// <summary>
/// Abbreviation plugin. Two-phase preprocessor:
/// <list type="number">
/// <item><description>Scan the source for <c>*[token]: definition</c> lines, record them, and strip the definition lines from the output.</description></item>
/// <item><description>Walk the stripped source and wrap every word-boundary occurrence of a known token in <c>&lt;abbr title="…"&gt;…&lt;/abbr&gt;</c>.</description></item>
/// </list>
/// Fenced-code regions and inline-code spans are skipped during the wrap phase.
/// </summary>
public sealed class AbbrPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override byte[] Name => "abbr"u8.ToArray();

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        AbbrRewriter.Rewrite(source, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, FilePath relativePath) =>
        Preprocess(source, writer);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;
}
