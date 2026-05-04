// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.CaretTilde;

/// <summary>
/// Caret + tilde plugin. Rewrites <c>^x^</c> as <c>&lt;sup&gt;</c>,
/// <c>^^x^^</c> as <c>&lt;ins&gt;</c>, <c>~x~</c> as <c>&lt;sub&gt;</c>,
/// and <c>~~x~~</c> as <c>&lt;del&gt;</c> — covering pymdownx.caret +
/// pymdownx.tilde defaults. Fenced and inline code are passed
/// through verbatim.
/// </summary>
public sealed class CaretTildePlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override byte[] Name => "caret-tilde"u8.ToArray();

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasCaretOrTilde(source);

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        CaretTildeRewriter.Rewrite(source, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, string relativePath) =>
        Preprocess(source, writer);
}
