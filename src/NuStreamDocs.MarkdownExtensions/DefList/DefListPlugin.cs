// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.DefList;

/// <summary>
/// Definition-list plugin. Rewrites
/// <c>term&#10;: definition</c> blocks into
/// <c>&lt;dl&gt;&lt;dt&gt;…&lt;/dt&gt;&lt;dd&gt;…&lt;/dd&gt;&lt;/dl&gt;</c>
/// HTML before the markdown renderer runs.
/// </summary>
public sealed class DefListPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override string Name => "deflist";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        DefListRewriter.Rewrite(source, writer);
    }
}
