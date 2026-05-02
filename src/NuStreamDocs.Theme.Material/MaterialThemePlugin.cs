// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material;

/// <summary>
/// Plugin that wraps every rendered page in the Material theme shell
/// and writes the theme's static assets at finalization time.
/// </summary>
/// <remarks>
/// The render hook takes the markdown-rendered HTML body
/// the core pipeline already produced, drops it into the
/// <c>{{{body}}}</c> slot of the Mustache page template, and rewrites
/// the page-output buffer in place. The finalize hook writes
/// the static asset bundle only when the option set selects
/// <see cref="MaterialAssetSource.Embedded"/>; the CDN mode skips the
/// write so the deploy artefact stays light.
/// </remarks>
public sealed class MaterialThemePlugin : ThemePluginBase<MaterialTheme, MaterialThemeOptions>
{
    /// <summary>Initializes a new instance of the <see cref="MaterialThemePlugin"/> class with default options.</summary>
    public MaterialThemePlugin()
        : this(MaterialThemeOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MaterialThemePlugin"/> class.</summary>
    /// <param name="options">Theme options.</param>
    public MaterialThemePlugin(in MaterialThemeOptions options)
        : base(options, MaterialTheme.Load())
    {
    }

    /// <inheritdoc/>
    public override string Name => "material-theme";

    /// <summary>Gets the loaded theme; exposed for tests.</summary>
    internal MaterialTheme Theme => LoadedTheme;
}
