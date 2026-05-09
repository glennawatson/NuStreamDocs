// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins.ExtraAssets;

/// <summary>
/// One extra-asset source contributed via the builder API. Construct via the per-kind factory
/// methods (<see cref="File"/>, <see cref="Inline"/>, <see cref="Embedded"/>, <see
/// cref="External"/>).
/// </summary>
public sealed class ExtraAssetSource
{
    /// <summary>Initializes a new instance of the <see cref="ExtraAssetSource"/> class.</summary>
    /// <param name="init">Per-kind init bundle.</param>
    private ExtraAssetSource(in ExtraAssetSourceInit init)
    {
        Kind = init.Kind;
        FilePath = init.FilePath;
        InlineBytes = init.InlineBytes;
        Assembly = init.Assembly;
        ResourceName = init.ResourceName;
        OutputName = init.OutputName;
        Url = init.Url;
        IsModule = init.IsModule;
    }

    /// <summary>Gets the source kind.</summary>
    public ExtraAssetSourceKind Kind { get; }

    /// <summary>Gets the disk path; <see cref="NuStreamDocs.Common.FilePath.IsEmpty"/> for non-<see cref="ExtraAssetSourceKind.File"/> kinds.</summary>
    public FilePath FilePath { get; }

    /// <summary>Gets the inline UTF-8 bytes; non-null only for <see cref="ExtraAssetSourceKind.Inline"/>.</summary>
    public byte[]? InlineBytes { get; }

    /// <summary>Gets the source assembly; non-null only for <see cref="ExtraAssetSourceKind.Embedded"/>.</summary>
    public Assembly? Assembly { get; }

    /// <summary>Gets the embedded-resource manifest name; non-null only for <see cref="ExtraAssetSourceKind.Embedded"/>.</summary>
    public string? ResourceName { get; }

    /// <summary>Gets the output file name written under <c>assets/extra/</c>; null for <see cref="ExtraAssetSourceKind.Url"/>.</summary>
    public string? OutputName { get; }

    /// <summary>Gets the external href; non-null only for <see cref="ExtraAssetSourceKind.Url"/>.</summary>
    public string? Url { get; }

    /// <summary>Gets a value indicating whether the JS asset should be loaded as an ES module (<c>type="module"</c>); ignored for CSS sources.</summary>
    public bool IsModule { get; }

    /// <summary>Creates a file-on-disk source.</summary>
    /// <param name="filePath">Absolute or relative path to a UTF-8 asset file. String literals convert via the implicit <see cref="NuStreamDocs.Common.FilePath"/> operator.</param>
    /// <returns>The source.</returns>
    public static ExtraAssetSource File(FilePath filePath)
    {
        if (filePath.IsEmpty)
        {
            throw new ArgumentException("File path must be non-empty.", nameof(filePath));
        }

        return new(new ExtraAssetSourceInit
        {
            Kind = ExtraAssetSourceKind.File,
            FilePath = filePath,
            OutputName = Path.GetFileName(filePath.Value),
        });
    }

    /// <summary>Creates an inline UTF-8 source.</summary>
    /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
    /// <param name="utf8Bytes">UTF-8 asset bytes.</param>
    /// <returns>The source.</returns>
    public static ExtraAssetSource Inline(string outputName, byte[] utf8Bytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputName);
        ArgumentNullException.ThrowIfNull(utf8Bytes);
        return new(new ExtraAssetSourceInit
        {
            Kind = ExtraAssetSourceKind.Inline,
            InlineBytes = utf8Bytes,
            OutputName = outputName,
        });
    }

    /// <summary>Creates an embedded-resource source.</summary>
    /// <param name="assembly">Assembly carrying the resource.</param>
    /// <param name="resourceName">Manifest resource name.</param>
    /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
    /// <returns>The source.</returns>
    public static ExtraAssetSource Embedded(Assembly assembly, string resourceName, string outputName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentException.ThrowIfNullOrEmpty(outputName);
        return new(new ExtraAssetSourceInit
        {
            Kind = ExtraAssetSourceKind.Embedded,
            Assembly = assembly,
            ResourceName = resourceName,
            OutputName = outputName,
        });
    }

    /// <summary>Creates an external-URL source. No asset is shipped — only a <c>&lt;link&gt;</c> or <c>&lt;script&gt;</c> tag.</summary>
    /// <param name="url">External href.</param>
    /// <returns>The source.</returns>
    public static ExtraAssetSource External(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        return new(new ExtraAssetSourceInit
        {
            Kind = ExtraAssetSourceKind.Url,
            Url = url,
        });
    }

    /// <summary>Returns a copy of this source flagged as an ES module so the head-extra emitter renders <c>type="module"</c>.</summary>
    /// <returns>A new <see cref="ExtraAssetSource"/> with <see cref="IsModule"/> set; ignored for CSS sources.</returns>
    public ExtraAssetSource AsModule() =>
        new(new ExtraAssetSourceInit
        {
            Kind = Kind,
            FilePath = FilePath,
            InlineBytes = InlineBytes,
            Assembly = Assembly,
            ResourceName = ResourceName,
            OutputName = OutputName,
            Url = Url,
            IsModule = true,
        });
}
