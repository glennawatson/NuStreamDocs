// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins.ExtraAssets;

/// <summary>Init-only bundle for <see cref="ExtraAssetSource"/>'s private ctor; callers should use the per-kind factory methods on <see cref="ExtraAssetSource"/>.</summary>
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
