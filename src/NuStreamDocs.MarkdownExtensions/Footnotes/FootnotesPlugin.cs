// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Footnotes;

/// <summary>
/// Footnotes plugin. Rewrites inline <c>[^id]</c> references and
/// block <c>[^id]: definition</c> entries into linked superscripts
/// plus an appended <c>&lt;section class="footnotes"&gt;</c>.
/// </summary>
public sealed class FootnotesPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override string Name => "footnotes";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        FootnotesRewriter.Rewrite(source, writer);
    }
}
