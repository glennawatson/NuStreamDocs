// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace NuStreamDocs.Plugins.ExtraAssets;

/// <summary>One extra-asset source contributed via the builder API.</summary>
/// <remarks>
/// Authored as a discriminated union so the builder-side API stays
/// fluent (one method per source kind) while the plugin-side
/// resolution stays one switch over <see cref="Kind"/>.
/// </remarks>
public sealed class ExtraAssetSource
{
    /// <summary>Initializes a new instance of the <see cref="ExtraAssetSource"/> class.</summary>
    /// <param name="kind">Source kind.</param>
    /// <param name="filePath">Disk path for <see cref="ExtraAssetSourceKind.File"/>; otherwise null.</param>
    /// <param name="inlineBytes">UTF-8 bytes for <see cref="ExtraAssetSourceKind.Inline"/>; otherwise null.</param>
    /// <param name="assembly">Assembly for <see cref="ExtraAssetSourceKind.Embedded"/>; otherwise null.</param>
    /// <param name="resourceName">Embedded-resource name; otherwise null.</param>
    /// <param name="outputName">File name written under <c>assets/extra/</c>; null for <see cref="ExtraAssetSourceKind.Url"/>.</param>
    /// <param name="url">External href for <see cref="ExtraAssetSourceKind.Url"/>; otherwise null.</param>
    private ExtraAssetSource(
        ExtraAssetSourceKind kind,
        string? filePath,
        byte[]? inlineBytes,
        Assembly? assembly,
        string? resourceName,
        string? outputName,
        string? url)
    {
        Kind = kind;
        FilePath = filePath;
        InlineBytes = inlineBytes;
        Assembly = assembly;
        ResourceName = resourceName;
        OutputName = outputName;
        Url = url;
    }

    /// <summary>Gets the source kind.</summary>
    public ExtraAssetSourceKind Kind { get; }

    /// <summary>Gets the disk path; non-null only for <see cref="ExtraAssetSourceKind.File"/>.</summary>
    public string? FilePath { get; }

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

    /// <summary>Creates a file-on-disk source.</summary>
    /// <param name="filePath">Absolute or relative path to a UTF-8 asset file.</param>
    /// <returns>The source.</returns>
    public static ExtraAssetSource File(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return new(ExtraAssetSourceKind.File, filePath, null, null, null, Path.GetFileName(filePath), null);
    }

    /// <summary>Creates an inline UTF-8 source.</summary>
    /// <param name="outputName">File name written under <c>assets/extra/</c>.</param>
    /// <param name="utf8Bytes">UTF-8 asset bytes.</param>
    /// <returns>The source.</returns>
    public static ExtraAssetSource Inline(string outputName, byte[] utf8Bytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputName);
        ArgumentNullException.ThrowIfNull(utf8Bytes);
        return new(ExtraAssetSourceKind.Inline, null, utf8Bytes, null, null, outputName, null);
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
        return new(ExtraAssetSourceKind.Embedded, null, null, assembly, resourceName, outputName, null);
    }

    /// <summary>Creates an external-URL source. No asset is shipped — only a <c>&lt;link&gt;</c> or <c>&lt;script&gt;</c> tag.</summary>
    /// <param name="url">External href.</param>
    /// <returns>The source.</returns>
    public static ExtraAssetSource External(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        return new(ExtraAssetSourceKind.Url, null, null, null, null, null, url);
    }
}
