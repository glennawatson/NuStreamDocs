// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MagicLink;

/// <summary>
/// Magic-link plugin. Pre-processes raw Markdown so that bare
/// <c>http://</c>, <c>https://</c>, <c>ftp://</c>, <c>ftps://</c>,
/// <c>mailto:</c>, and <c>www.</c> URLs become CommonMark
/// autolinks (<c>&lt;url&gt;</c>) — which the inline renderer
/// already turns into <c>&lt;a href&gt;</c> tags.
/// </summary>
/// <remarks>
/// Mirrors the URL-autolink slice of pymdownx.magiclink. Provider-
/// specific shortcodes (<c>@user</c>, <c>repo#123</c>, commit
/// hashes) require an explicit GitHub/GitLab provider hook and
/// are not in scope for the default-on behavior Zensical enables
/// out of the box.
/// </remarks>
public sealed class MagicLinkPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override byte[] Name => "magiclink"u8.ToArray();

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        MagicLinkRewriter.Rewrite(source, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, FilePath relativePath) =>
        Preprocess(source, writer);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;
}
