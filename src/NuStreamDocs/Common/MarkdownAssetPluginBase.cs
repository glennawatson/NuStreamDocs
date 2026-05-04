// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Common;

/// <summary>
/// Convenience base class for markdown-extension plugins that ship a
/// single CSS asset and a single <c>&lt;link rel="stylesheet"&gt;</c>
/// head fragment. Wraps the boilerplate that Tabs/CheckList/Admonition/
/// Details all duplicated.
/// </summary>
public abstract class MarkdownAssetPluginBase : DocPluginBase, IMarkdownPreprocessor, IStaticAssetProvider, IHeadExtraProvider
{
    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets => [(AssetPath, StylesheetBytes)];

    /// <summary>Gets the forward-slash relative path the css asset is written to.</summary>
    protected abstract FilePath AssetPath { get; }

    /// <summary>Gets the UTF-8 stylesheet shipped to every site under <see cref="AssetPath"/>.</summary>
    protected abstract byte[] StylesheetBytes { get; }

    /// <summary>Gets the UTF-8 <c>&lt;link rel="stylesheet"&gt;</c> head fragment for the asset.</summary>
    protected abstract byte[] HeadLink { get; }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(HeadLink);
    }

    /// <inheritdoc/>
    public abstract void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer);

    /// <inheritdoc/>
    public virtual void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, FilePath relativePath) =>
        Preprocess(source, writer);

    /// <inheritdoc/>
    public virtual bool NeedsRewrite(ReadOnlySpan<byte> source) => true;
}
