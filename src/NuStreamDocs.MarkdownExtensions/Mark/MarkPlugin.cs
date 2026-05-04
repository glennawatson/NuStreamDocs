// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Mark;

/// <summary>
/// Mark plugin. Rewrites inline <c>==text==</c> spans into
/// <c>&lt;mark&gt;text&lt;/mark&gt;</c> before the markdown renderer
/// runs. Fenced-code regions and inline-code spans are left alone.
/// </summary>
public sealed class MarkPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override byte[] Name => "mark"u8.ToArray();

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasMarkSpan(source);

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        MarkRewriter.Rewrite(source, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, string relativePath) =>
        Preprocess(source, writer);
}
