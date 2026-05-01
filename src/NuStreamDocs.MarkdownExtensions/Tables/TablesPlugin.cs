// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Tables;

/// <summary>
/// GitHub-flavoured tables plugin. Rewrites pipe-delimited table
/// blocks into <c>&lt;table&gt;</c> HTML before the markdown
/// renderer runs.
/// </summary>
public sealed class TablesPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override string Name => "tables";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        TablesRewriter.Rewrite(source, writer);
    }
}
