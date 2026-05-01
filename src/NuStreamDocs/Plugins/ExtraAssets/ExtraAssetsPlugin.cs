// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Plugins.ExtraAssets;

/// <summary>
/// Plugin that ships caller-supplied stylesheet and script assets,
/// and emits matching <c>&lt;link&gt;</c> / <c>&lt;script&gt;</c>
/// tags into every page's <c>&lt;head&gt;</c>.
/// </summary>
/// <remarks>
/// The builder API folds repeated <c>AddExtraCss</c> / <c>AddExtraJs</c>
/// calls onto the same instance so registration produces one bundle,
/// not one plugin per call. Sources are resolved lazily during
/// <see cref="OnConfigureAsync"/> so file reads happen alongside the rest
/// of the build's I/O.
/// </remarks>
public sealed class ExtraAssetsPlugin : IDocPlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>Forward-slash directory the resolved assets are written under.</summary>
    private const string AssetDirectory = "assets/extra";

    /// <summary>CSS sources accumulated through the builder API.</summary>
    private readonly List<ExtraAssetSource> _cssSources = [];

    /// <summary>JS sources accumulated through the builder API.</summary>
    private readonly List<ExtraAssetSource> _jsSources = [];

    /// <summary>Resolved CSS assets ready to be shipped (file/inline/embedded only).</summary>
    private readonly List<(string Path, byte[] Bytes)> _cssAssets = [];

    /// <summary>Resolved JS assets ready to be shipped (file/inline/embedded only).</summary>
    private readonly List<(string Path, byte[] Bytes)> _jsAssets = [];

    /// <summary>UTF-8 head fragment composed once during <see cref="OnConfigureAsync"/>.</summary>
    private byte[] _headFragment = [];

    /// <inheritdoc/>
    public string Name => "extra-assets";

    /// <inheritdoc/>
    public (string Path, byte[] Bytes)[] StaticAssets => [.. _cssAssets, .. _jsAssets];

    /// <summary>Appends a CSS source. Called by the builder API; folds onto the existing instance when one is already registered.</summary>
    /// <param name="source">Source to add.</param>
    public void AddCss(ExtraAssetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _cssSources.Add(source);
    }

    /// <summary>Appends a JS source. Called by the builder API; folds onto the existing instance when one is already registered.</summary>
    /// <param name="source">Source to add.</param>
    public void AddJs(ExtraAssetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _jsSources.Add(source);
    }

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        Resolve(_cssSources, _cssAssets);
        Resolve(_jsSources, _jsAssets);
        _headFragment = ComposeHead();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinaliseAsync(PluginFinaliseContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (_headFragment.Length is 0)
        {
            return;
        }

        writer.Write(_headFragment);
    }

    /// <summary>Resolves the bytes for every shippable source into <paramref name="assets"/>.</summary>
    /// <param name="sources">Source list.</param>
    /// <param name="assets">Output list of <c>(relativePath, bytes)</c> pairs.</param>
    private static void Resolve(List<ExtraAssetSource> sources, List<(string Path, byte[] Bytes)> assets)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            var src = sources[i];
            if (src.Kind is ExtraAssetSourceKind.Url)
            {
                continue;
            }

            var bytes = ReadBytes(src);
            assets.Add(($"{AssetDirectory}/{src.OutputName}", bytes));
        }
    }

    /// <summary>Reads the bytes for one shippable source.</summary>
    /// <param name="source">Source to resolve.</param>
    /// <returns>UTF-8 asset bytes.</returns>
    private static byte[] ReadBytes(ExtraAssetSource source) => source.Kind switch
    {
        ExtraAssetSourceKind.File => File.ReadAllBytes(source.FilePath!),
        ExtraAssetSourceKind.Inline => source.InlineBytes!,
        ExtraAssetSourceKind.Embedded => ReadEmbedded(source),
        _ => throw new InvalidOperationException($"Source kind {source.Kind} cannot be resolved to bytes."),
    };

    /// <summary>Reads an embedded resource into a fresh byte array.</summary>
    /// <param name="source">Embedded-resource source.</param>
    /// <returns>UTF-8 asset bytes.</returns>
    private static byte[] ReadEmbedded(ExtraAssetSource source) => EmbeddedResourceReader.Read(source);

    /// <summary>Writes one <c>&lt;link rel="stylesheet" href="…"&gt;</c> tag.</summary>
    /// <param name="source">CSS source.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteCssLink(ExtraAssetSource source, IBufferWriter<byte> writer)
    {
        var href = source.Kind is ExtraAssetSourceKind.Url
            ? source.Url!
            : $"/{AssetDirectory}/{source.OutputName}";
        writer.Write("<link rel=\"stylesheet\" href=\""u8);
        writer.Write(Encoding.UTF8.GetBytes(href));
        writer.Write("\">"u8);
    }

    /// <summary>Writes one <c>&lt;script src="…" defer&gt;</c> tag.</summary>
    /// <param name="source">JS source.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteJsScript(ExtraAssetSource source, IBufferWriter<byte> writer)
    {
        var src = source.Kind is ExtraAssetSourceKind.Url
            ? source.Url!
            : $"/{AssetDirectory}/{source.OutputName}";
        writer.Write("<script src=\""u8);
        writer.Write(Encoding.UTF8.GetBytes(src));
        writer.Write("\" defer></script>"u8);
    }

    /// <summary>Composes the head fragment: one <c>&lt;link&gt;</c> per CSS source, one <c>&lt;script&gt;</c> per JS source.</summary>
    /// <returns>UTF-8 head fragment bytes.</returns>
    private byte[] ComposeHead()
    {
        if (_cssSources.Count is 0 && _jsSources.Count is 0)
        {
            return [];
        }

        var writer = new ArrayBufferWriter<byte>();
        for (var i = 0; i < _cssSources.Count; i++)
        {
            WriteCssLink(_cssSources[i], writer);
        }

        for (var i = 0; i < _jsSources.Count; i++)
        {
            WriteJsScript(_jsSources[i], writer);
        }

        return [.. writer.WrittenSpan];
    }
}
