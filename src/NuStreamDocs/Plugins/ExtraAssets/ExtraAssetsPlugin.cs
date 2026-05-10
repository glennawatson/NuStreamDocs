// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins.ExtraAssets;

/// <summary>Plugin that ships caller-supplied stylesheet and script assets, emitting matching <c>&lt;link&gt;</c> / <c>&lt;script&gt;</c> tags into every page's <c>&lt;head&gt;</c>.</summary>
public sealed class ExtraAssetsPlugin : IBuildConfigurePlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>CSS sources accumulated through the builder API.</summary>
    private readonly List<ExtraAssetSource> _cssSources = [];

    /// <summary>JS sources accumulated through the builder API.</summary>
    private readonly List<ExtraAssetSource> _jsSources = [];

    /// <summary>Resolved CSS assets ready to be shipped (file/inline/embedded only).</summary>
    private readonly List<(FilePath Path, byte[] Bytes)> _cssAssets = [];

    /// <summary>Resolved JS assets ready to be shipped (file/inline/embedded only).</summary>
    private readonly List<(FilePath Path, byte[] Bytes)> _jsAssets = [];

    /// <summary>UTF-8 head fragment composed once during <see cref="ConfigureAsync"/>.</summary>
    private byte[] _headFragment = [];

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "extra-assets"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets => [.. _cssAssets, .. _jsAssets];

    /// <summary>Gets the UTF-8 leading-slash + asset directory + trailing slash for site-relative URL byte emit.</summary>
    private static ReadOnlySpan<byte> AssetDirectoryUrlPrefixUtf8 => "/assets/extra/"u8;

    /// <summary>Appends a CSS source. Called by the builder API; folds onto the existing instance when one is already registered.</summary>
    /// <param name="source">Source to add.</param>
    public void AddCss(ExtraAssetSource source)
    {
        _cssSources.Add(source);
    }

    /// <summary>Appends a JS source. Called by the builder API; folds onto the existing instance when one is already registered.</summary>
    /// <param name="source">Source to add.</param>
    public void AddJs(ExtraAssetSource source)
    {
        _jsSources.Add(source);
    }

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        Resolve(_cssSources, _cssAssets);
        Resolve(_jsSources, _jsAssets);
        _headFragment = ComposeHead();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        if (_headFragment.Length is 0)
        {
            return;
        }

        writer.Write(_headFragment);
    }

    /// <summary>Resolves the bytes for every shippable source into <paramref name="assets"/>.</summary>
    /// <param name="sources">Source list.</param>
    /// <param name="assets">Output list of <c>(relativePath, bytes)</c> pairs.</param>
    private static void Resolve(List<ExtraAssetSource> sources, List<(FilePath Path, byte[] Bytes)> assets)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            var src = sources[i];
            if (src.Kind is ExtraAssetSourceKind.Url)
            {
                continue;
            }

            var bytes = ReadBytes(src);
            assets.Add((new(ComposeAssetPath(src.OutputName!)), bytes));
        }
    }

    /// <summary>Reads the bytes for one shippable source.</summary>
    /// <param name="source">Source to resolve.</param>
    /// <returns>UTF-8 asset bytes.</returns>
    private static byte[] ReadBytes(ExtraAssetSource source) => source.Kind switch
    {
        ExtraAssetSourceKind.File => File.ReadAllBytes(source.FilePath.Value),
        ExtraAssetSourceKind.Inline => source.InlineBytes!,
        ExtraAssetSourceKind.Embedded => ReadEmbedded(source),
        _ => throw new InvalidOperationException($"Source kind {source.Kind} cannot be resolved to bytes.")
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
        writer.Write("<link rel=\"stylesheet\" href=\""u8);
        WriteAssetHref(source, writer);
        writer.Write("\">"u8);
    }

    /// <summary>Writes one <c>&lt;script src="…" defer&gt;</c> (or <c>type="module"</c>) tag.</summary>
    /// <param name="source">JS source.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteJsScript(ExtraAssetSource source, IBufferWriter<byte> writer)
    {
        writer.Write("<script src=\""u8);
        WriteAssetHref(source, writer);

        // ES modules are deferred by spec, so emit `type="module"` instead of `defer`.
        // Standard scripts get `defer` so they execute after the document is parsed but
        // before DOMContentLoaded — the safest default for asset registration order.
        writer.Write(source.IsModule ? "\" type=\"module\"></script>"u8 : "\" defer></script>"u8);
    }

    /// <summary>Writes the site-relative or absolute URL for <paramref name="source"/> as UTF-8 bytes — no intermediate string.</summary>
    /// <param name="source">Asset source.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteAssetHref(ExtraAssetSource source, IBufferWriter<byte> writer)
    {
        if (source.Kind is ExtraAssetSourceKind.Url)
        {
            writer.Write(Encoding.UTF8.GetBytes(source.Url!));
            return;
        }

        writer.Write(AssetDirectoryUrlPrefixUtf8);
        writer.Write(Encoding.UTF8.GetBytes(source.OutputName!));
    }

    /// <summary>Composes the asset's site-relative file path (<c>assets/extra/&lt;outputName&gt;</c>) via the project's <see cref="StringCompose"/> helper.</summary>
    /// <param name="outputName">Caller-supplied output filename (already validated non-null upstream).</param>
    /// <returns>Composed path string suitable for <see cref="FilePath"/>.</returns>
    private static string ComposeAssetPath(string outputName) => StringCompose.Concat("assets/extra/", outputName);

    /// <summary>Composes the head fragment: one <c>&lt;link&gt;</c> per CSS source, one <c>&lt;script&gt;</c> per JS source.</summary>
    /// <returns>UTF-8 head fragment bytes.</returns>
    private byte[] ComposeHead()
    {
        if (_cssSources.Count is 0 && _jsSources.Count is 0)
        {
            return [];
        }

        ArrayBufferWriter<byte> writer = new();
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
