// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.MagicLink;

/// <summary>
/// Magic-link plugin — wraps bare URLs (<c>http</c>/<c>https</c>/<c>ftp</c>/<c>ftps</c>/
/// <c>mailto</c>) as CommonMark autolinks, and optionally expands GitHub <c>#NNN</c> /
/// <c>@user</c> shortrefs.
/// </summary>
public sealed class MagicLinkPlugin : IPagePreRenderPlugin
{
    /// <summary>Configured options.</summary>
    private readonly MagicLinkOptions _options;

    /// <summary>Initializes a new instance of the <see cref="MagicLinkPlugin"/> class with default (URL-only) settings.</summary>
    public MagicLinkPlugin()
        : this(new())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MagicLinkPlugin"/> class with caller-supplied options.</summary>
    /// <param name="options">Options controlling shortref expansion.</param>
    public MagicLinkPlugin(MagicLinkOptions options) => _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "magiclink"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        MagicLinkRewriter.Rewrite(context.Source, context.Output, _options.DefaultRepo, _options.ExpandUserMentions);
}
