// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.CheckList;

/// <summary>
/// Check-list plugin. Rewrites <c>- [ ] item</c> and <c>- [x] item</c>
/// at list-item starts into inline disabled-checkbox HTML so the
/// markdown renderer passes them through unchanged.
/// </summary>
public sealed class CheckListPlugin : MarkdownAssetPluginBase
{
    /// <summary>UTF-8 head-link snippet pulled into every rendered page.</summary>
    private static readonly byte[] LinkBytes =
        [.. """<link rel="stylesheet" href="/assets/extensions/checklist.css">"""u8];

    /// <summary>UTF-8 stylesheet shipped to every site.</summary>
    private static readonly byte[] CssBytes =
        [.. """
.task-list-item{list-style:none;margin-left:-1.4em}
.task-list-item input[type=checkbox]{margin-right:.4em;vertical-align:middle}
"""u8];

    /// <inheritdoc/>
    public override byte[] Name => "checklist"u8.ToArray();

    /// <inheritdoc/>
    protected override FilePath AssetPath => new("assets/extensions/checklist.css");

    /// <inheritdoc/>
    protected override byte[] StylesheetBytes => CssBytes;

    /// <inheritdoc/>
    protected override byte[] HeadLink => LinkBytes;

    /// <inheritdoc/>
    public override bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasCheckListBullet(source);

    /// <inheritdoc/>
    public override void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        CheckListRewriter.Rewrite(source, writer);
    }
}
