// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Optional contract for plugins shipping static assets (CSS, JS, fonts, images) into the site
/// output. Theme plugins discover providers during finalize and write each
/// <c>(relativePath, bytes)</c> pair under the output root.
/// </summary>
public interface IStaticAssetProvider
{
    /// <summary>Gets the static assets this plugin contributes.</summary>
    /// <remarks>Pairs of forward-slash relative paths and their UTF-8 bytes.</remarks>
    (FilePath Path, byte[] Bytes)[] StaticAssets { get; }
}
