// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Keys;

/// <summary>
/// Keys plugin. Rewrites <c>++ctrl+alt+del++</c> shortcuts into the
/// structured keys-span markup pymdownx.keys produces, so theme
/// stylesheets pick them up by their <c>.keys</c> /
/// <c>.key-<i>name</i></c> classes.
/// </summary>
/// <remarks>
/// Output shape:
/// <code>
/// &lt;span class="keys"&gt;
///   &lt;kbd class="key-ctrl"&gt;Ctrl&lt;/kbd&gt;
///   &lt;span&gt;+&lt;/span&gt;
///   &lt;kbd class="key-alt"&gt;Alt&lt;/kbd&gt;
///   &lt;span&gt;+&lt;/span&gt;
///   &lt;kbd class="key-delete"&gt;Delete&lt;/kbd&gt;
/// &lt;/span&gt;
/// </code>
/// Unrecognised key tokens still render through with their literal
/// label and a sanitised class derived from the token bytes.
/// </remarks>
public sealed class KeysPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override string Name => "keys";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        KeysRewriter.Rewrite(source, writer);
    }
}
