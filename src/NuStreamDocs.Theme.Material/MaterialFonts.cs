// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Fonts;

namespace NuStreamDocs.Theme.Material;

/// <summary>Self-hosted-font defaults matching the Material (mkdocs-material) theme: Roboto for body text, Roboto Mono for code. Pass to <c>builder.UseFonts(...)</c>.</summary>
public static class MaterialFonts
{
    /// <summary>Light weight.</summary>
    private const int Light = 300;

    /// <summary>Regular weight.</summary>
    private const int Regular = 400;

    /// <summary>Medium weight.</summary>
    private const int Medium = 500;

    /// <summary>Bold weight.</summary>
    private const int Bold = 700;

    /// <summary>Gets the font configuration: Roboto (driving <c>--md-text-font</c>) and Roboto Mono (driving <c>--md-code-font</c>), latin + latin-ext, self-hosted from Google Fonts.</summary>
    public static FontsOptions Default { get; } = FontsOptions.Default
        .AddGoogleFont("Roboto"u8, Light, Regular, Medium, Bold)
        .WithThemeVariables([.. "--md-text-font"u8])
        .AddGoogleFont("Roboto Mono"u8, Regular, Bold)
        .WithFallback(GenericFontFamily.Monospace)
        .WithoutPreload()
        .WithThemeVariables([.. "--md-code-font"u8]);
}
