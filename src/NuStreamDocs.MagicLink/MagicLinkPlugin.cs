// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
public sealed class MagicLinkPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "magiclink"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        MagicLinkRewriter.Rewrite(context.Source, context.Output);
}
