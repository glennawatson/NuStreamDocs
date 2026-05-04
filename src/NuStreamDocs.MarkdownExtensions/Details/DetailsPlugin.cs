// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.Details;

/// <summary>
/// Collapsible-details plugin. Rewrites
/// <c>??? type "title"</c> (collapsed) and <c>???+ type "title"</c>
/// (open) blocks into native <c>&lt;details&gt;</c> elements before
/// the markdown renderer runs.
/// </summary>
public sealed class DetailsPlugin : MarkdownAssetPluginBase
{
    /// <summary>UTF-8 head-link snippet pulled into every rendered page.</summary>
    private static readonly byte[] LinkBytes =
        [.. """<link rel="stylesheet" href="/assets/extensions/details.css">"""u8];

    /// <summary>UTF-8 stylesheet shipped to every site.</summary>
    private static readonly byte[] CssBytes =
        [.. """
            details{border-left:4px solid #448aff;background:rgba(68,138,255,.06);padding:.6em 1em;margin:1em 0;border-radius:.2em}
            details>summary{font-weight:700;cursor:pointer;list-style:none}
            details>summary::-webkit-details-marker{display:none}
            details>summary::before{content:"\25B8";display:inline-block;margin-right:.4em;transition:transform .15s ease}
            details[open]>summary::before{transform:rotate(90deg)}
            details.tip,details.hint{border-left-color:#00bfa5;background:rgba(0,191,165,.06)}
            details.warning,details.caution,details.attention{border-left-color:#ff9100;background:rgba(255,145,0,.06)}
            details.danger,details.error{border-left-color:#ff1744;background:rgba(255,23,68,.06)}
            details.info,details.todo{border-left-color:#00b8d4;background:rgba(0,184,212,.06)}
            details.success,details.check{border-left-color:#00c853;background:rgba(0,200,83,.06)}
            details.quote,details.cite{border-left-color:#9e9e9e;background:rgba(158,158,158,.06)}
            """u8];

    /// <inheritdoc/>
    public override byte[] Name => "details"u8.ToArray();

    /// <inheritdoc/>
    protected override FilePath AssetPath => new("assets/extensions/details.css");

    /// <inheritdoc/>
    protected override byte[] StylesheetBytes => CssBytes;

    /// <inheritdoc/>
    protected override byte[] HeadLink => LinkBytes;

    /// <inheritdoc/>
    public override bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasDetailsOpener(source);

    /// <inheritdoc/>
    public override void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        DetailsRewriter.Rewrite(source, writer);
    }
}
