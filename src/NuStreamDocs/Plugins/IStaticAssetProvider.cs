// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Optional companion contract for plugins that ship static assets
/// (CSS, JS, fonts, images) into the site output alongside the active
/// theme's own bundle.
/// </summary>
/// <remarks>
/// The canonical use case is markdown-extension plugins
/// (admonitions, tabs, details, mermaid) and icon plugins that need
/// to drop a stylesheet or script into the output tree without owning
/// a theme of their own. Theme plugins discover providers during
/// <see cref="IBuildFinalizePlugin.FinalizeAsync"/> by walking
/// <see cref="BuildFinalizeContext.Plugins"/> and write each
/// <c>(relativePath, bytes)</c> tuple under the output root.
/// <para>
/// Both <c>NuStreamDocs.Theme.Material</c> and
/// <c>NuStreamDocs.Theme.Material3</c> honor this contract, so a
/// markdown-extension plugin written once works under either theme
/// without recompilation.
/// </para>
/// </remarks>
public interface IStaticAssetProvider
{
    /// <summary>Gets the static assets this plugin contributes.</summary>
    /// <remarks>Pairs of forward-slash relative paths and their UTF-8 bytes.</remarks>
    (FilePath Path, byte[] Bytes)[] StaticAssets { get; }
}
