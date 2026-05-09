// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins.ExtraAssets;

/// <summary>
/// Init-only bundle for <see cref="ExtraAssetSource"/>'s private ctor — collapses
/// the eight per-kind fields into a single value so the ctor surface stays
/// 1-arg even as new flags (modules, integrity hashes, …) get added.
/// </summary>
/// <remarks>
/// Internal because callers should reach for <see cref="ExtraAssetSource"/>'s
/// per-kind factory methods (<c>File</c>, <c>Inline</c>, <c>Embedded</c>,
/// <c>External</c>) — this struct is purely a constructor-shape detail.
/// </remarks>
internal readonly record struct ExtraAssetSourceInit
{
    /// <summary>Gets the source kind.</summary>
    public ExtraAssetSourceKind Kind { get; init; }

    /// <summary>Gets the disk path; default for non-<see cref="ExtraAssetSourceKind.File"/> kinds.</summary>
    public FilePath FilePath { get; init; }

    /// <summary>Gets the inline UTF-8 bytes; non-null only for <see cref="ExtraAssetSourceKind.Inline"/>.</summary>
    public byte[]? InlineBytes { get; init; }

    /// <summary>Gets the source assembly; non-null only for <see cref="ExtraAssetSourceKind.Embedded"/>.</summary>
    public Assembly? Assembly { get; init; }

    /// <summary>Gets the embedded-resource manifest name; non-null only for <see cref="ExtraAssetSourceKind.Embedded"/>.</summary>
    public string? ResourceName { get; init; }

    /// <summary>Gets the output file name written under <c>assets/extra/</c>; null for <see cref="ExtraAssetSourceKind.Url"/>.</summary>
    public string? OutputName { get; init; }

    /// <summary>Gets the external href; non-null only for <see cref="ExtraAssetSourceKind.Url"/>.</summary>
    public string? Url { get; init; }

    /// <summary>Gets a value indicating whether the JS asset should be loaded as an ES module (<c>type="module"</c>); ignored for CSS sources.</summary>
    public bool IsModule { get; init; }
}
