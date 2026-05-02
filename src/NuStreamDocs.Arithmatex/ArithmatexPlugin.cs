// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Arithmatex;

/// <summary>
/// Arithmatex plugin (pymdownx.arithmatex generic mode).
/// Replaces inline <c>$x$</c> with
/// <c>&lt;span class="arithmatex"&gt;\(x\)&lt;/span&gt;</c> and
/// block <c>$$x$$</c> with
/// <c>&lt;div class="arithmatex"&gt;\[x\]&lt;/div&gt;</c> so a
/// client-side MathJax/KaTeX renderer can pick them up.
/// </summary>
/// <remarks>
/// Inline boundaries follow pymdownx defaults: an opening <c>$</c>
/// must not be immediately followed by whitespace, the closing
/// <c>$</c> must not be preceded by whitespace and must not be
/// followed by a digit (so prices like <c>$5</c> never trigger).
/// Fenced and inline-code regions pass through verbatim.
/// </remarks>
public sealed class ArithmatexPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override string Name => "arithmatex";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArithmatexRewriter.Rewrite(source, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, string relativePath) =>
        Preprocess(source, writer);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;
}
