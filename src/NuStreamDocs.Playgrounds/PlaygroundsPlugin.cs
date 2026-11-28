// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Playgrounds;

/// <summary>Ships playground CSS/JS assets and rewrites <c>http-request</c> and <c>playground</c> blocks in rendered HTML.</summary>
[System.Diagnostics.DebuggerDisplay("PlaygroundsPlugin: {Name}")]
public sealed class PlaygroundsPlugin : IPagePostRenderPlugin, IHeadExtraProvider, IStaticAssetProvider
{
    /// <summary>Post-render priority offset running after <c>SuperFencesPlugin</c>.</summary>
    private const int PostRenderOffset = 20;

    /// <summary>Site-relative stylesheet path.</summary>
    private static readonly FilePath CssAssetPath = new("assets/stylesheets/nstd-playgrounds.css");

    /// <summary>Site-relative JavaScript path.</summary>
    private static readonly FilePath JsAssetPath = new("assets/javascripts/nstd-playgrounds.js");

    /// <summary>Configured options.</summary>
    private readonly PlaygroundsOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PlaygroundsPlugin"/> class with default options.</summary>
    public PlaygroundsPlugin()
        : this(PlaygroundsOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PlaygroundsPlugin"/> class.</summary>
    /// <param name="options">Playground options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public PlaygroundsPlugin(PlaygroundsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        StaticAssets = BuildStaticAssets(options);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "playgrounds"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Normal, PostRenderOffset);

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => PlaygroundsHtmlRewriter.NeedsRewrite(html);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PostRender(in PagePostRenderContext context) =>
        PlaygroundsHtmlRewriter.Rewrite(context.Html, _options.EnableInteractiveTryIt, context.Output);

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        if (!_options.IncludeAssets)
        {
            return;
        }

        writer.Write("<link rel=\"stylesheet\" href=\"/assets/stylesheets/nstd-playgrounds.css\">\n"u8);
        if (!_options.EnableInteractiveTryIt)
        {
            return;
        }

        writer.Write("<script src=\"/assets/javascripts/nstd-playgrounds.js\" defer></script>\n"u8);
    }

    /// <summary>Builds the static asset list matching <paramref name="options"/>.</summary>
    /// <param name="options">Playground options.</param>
    /// <returns>Static asset tuples.</returns>
    private static (FilePath Path, byte[] Bytes)[] BuildStaticAssets(PlaygroundsOptions options)
    {
        if (!options.IncludeAssets)
        {
            return [];
        }

        return options.EnableInteractiveTryIt
            ? [(CssAssetPath, PlaygroundsAssets.CssBytes.ToArray()), (JsAssetPath, PlaygroundsAssets.JsBytes.ToArray())]
            : [(CssAssetPath, PlaygroundsAssets.CssBytes.ToArray())];
    }
}
