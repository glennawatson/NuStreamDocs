// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Videos;

/// <summary>Expands Markdown video URLs and <c>![title](url)</c> media links across common video platforms into responsive embeds.</summary>
[System.Diagnostics.DebuggerDisplay("VideoPlugin: {Name}")]
public sealed class VideoPlugin : IBuildConfigurePlugin, IPagePreRenderPlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>Responsive aspect-ratio stylesheet emitted when <see cref="VideoOptions.IncludeStylesheet"/> is enabled.</summary>
    private static readonly byte[] CssBytes =
    [
        .. """
           .nd-video{position:relative;width:100%;aspect-ratio:16/9;margin:1em 0;border-radius:.25rem;overflow:hidden;background:#000}
           .nd-video--shorts{max-width:22.5rem;aspect-ratio:9/16;margin-inline:auto}
           .nd-video>iframe,.nd-video>video{position:absolute;inset:0;width:100%;height:100%;border:0;display:block}
           """u8
    ];

    /// <summary>Relative output path for the bundled video stylesheet.</summary>
    private static readonly FilePath AssetFilePath = new("assets/extensions/video.css");

    /// <summary>Cached static asset array when stylesheet emission is enabled.</summary>
    private static readonly (FilePath Path, byte[] Bytes)[] DefaultStaticAssets = [(AssetFilePath, CssBytes)];

    /// <summary>Default Twitch <c>parent</c> host when neither options nor site URL supplies one.</summary>
    private static readonly byte[] DefaultTwitchParentBytes = [.. "localhost"u8];

    /// <summary>Configured options.</summary>
    private readonly VideoOptions _options;

    /// <summary>Resolved Twitch <c>parent</c> host bytes.</summary>
    private byte[] _resolvedTwitchParent;

    /// <summary>Initializes a new instance of the <see cref="VideoPlugin"/> class with <see cref="VideoOptions.Default"/>.</summary>
    public VideoPlugin()
        : this(VideoOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="VideoPlugin"/> class.</summary>
    /// <param name="options">Plugin configuration.</param>
    public VideoPlugin(in VideoOptions options)
    {
        _options = options;
        _resolvedTwitchParent = options.TwitchParent is [_, ..]
            ? options.TwitchParent
            : DefaultTwitchParentBytes;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "videos"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => new(PluginBand.Late);

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets =>
        _options.IncludeStylesheet ? DefaultStaticAssets : [];

    /// <summary>Gets the stylesheet link injected into <c>&lt;head&gt;</c> when <see cref="VideoOptions.IncludeStylesheet"/> is enabled.</summary>
    private static ReadOnlySpan<byte> StylesheetLinkBytes =>
        """<link rel="stylesheet" href="/assets/extensions/video.css">"""u8;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_options.TwitchParent is []
            && context.SiteUrl is [_, ..]
            && VideoUrlProbe.TryExtractHost(context.SiteUrl, out var siteHost))
        {
            _resolvedTwitchParent = siteHost.ToArray();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        _options.Providers is not VideoProviders.None
        && (_options.EmbedBareUrls || _options.EmbedImageLinks || _options.EmbedStandaloneLinks)
        && VideoUrlProbe.MayContainEmbeddableUrl(source, _options.Providers);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PreRender(in PagePreRenderContext context) =>
        VideoRewriter.Rewrite(context.Source, context.Output, _options, _resolvedTwitchParent);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        if (!_options.IncludeStylesheet)
        {
            return;
        }

        Utf8StringWriter.Write(writer, StylesheetLinkBytes);
    }
}
