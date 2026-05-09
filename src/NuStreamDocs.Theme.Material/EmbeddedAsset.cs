// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using NuStreamDocs.Common;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material;

/// <summary>Loads Material theme assets from the assembly's embedded-resource table.</summary>
public static class EmbeddedAsset
{
    /// <summary>Root namespace prefix MSBuild prepends to every embedded resource.</summary>
    private const string ResourcePrefix = "NuStreamDocs.Theme.Material.";

    /// <summary>The owning assembly; resources live alongside this type.</summary>
    private static readonly Assembly Owning = typeof(EmbeddedAsset).Assembly;

    /// <summary>Reads <paramref name="relativePath"/> as a UTF-8 byte array.</summary>
    /// <param name="relativePath">Forward-slashed path under the <c>Templates/</c> root.</param>
    /// <returns>The asset bytes.</returns>
    public static byte[] ReadBytes(FilePath relativePath)
        => EmbeddedAssetLoader.ReadBytes(Owning, ResourcePrefix, relativePath);

    /// <summary>Translates a folder-path asset key to the MSBuild resource name.</summary>
    /// <param name="relativePath">Forward-slashed path under <c>Templates/</c>.</param>
    /// <returns>The fully-qualified manifest resource name.</returns>
    public static byte[] ToResourceName(FilePath relativePath)
        => EmbeddedAssetLoader.ToResourceName(ResourcePrefix, relativePath);
}
