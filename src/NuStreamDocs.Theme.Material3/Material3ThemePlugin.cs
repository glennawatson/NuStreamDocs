// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3;

/// <summary>
/// Plugin that wraps every rendered page in the Material 3 shell and
/// writes the theme's static assets at finalization time.
/// </summary>
public sealed class Material3ThemePlugin : ThemePluginBase<Material3Theme, Material3ThemeOptions>
{
    /// <summary>Initializes a new instance of the <see cref="Material3ThemePlugin"/> class with default options.</summary>
    public Material3ThemePlugin()
        : this(Material3ThemeOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Material3ThemePlugin"/> class.</summary>
    /// <param name="options">Theme options.</param>
    public Material3ThemePlugin(in Material3ThemeOptions options)
        : base(options, Material3Theme.Load())
    {
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Name => "material3-theme"u8;

    /// <summary>Gets the loaded theme; exposed for tests.</summary>
    internal Material3Theme Theme => LoadedTheme;
}
