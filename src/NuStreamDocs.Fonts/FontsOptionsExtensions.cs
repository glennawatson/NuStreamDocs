// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>Fluent helpers for building <see cref="FontsOptions"/>.</summary>
public static class FontsOptionsExtensions
{
    /// <summary>Default numeric font weight when a face declares none.</summary>
    private const int DefaultWeight = 400;

    /// <summary>Extension members for <c>FontsOptions</c>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in FontsOptions options)
    {

        /// <summary>Adds a Google Fonts family (weight 400 unless given otherwise), default subsets, <c>font-display: swap</c>, sans-serif fallback.</summary>
        /// <param name="family">UTF-8 CSS family name (e.g. <c>"Source Sans 3"u8</c>).</param>
        /// <param name="weights">Numeric weights to fetch; empty fetches weight 400.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FontsOptions AddGoogleFont(
            ReadOnlySpan<byte> family,
            params int[] weights) =>
            AddFace(options, MakeFace(family.ToArray(), FontProviderKind.Google, NormalizeWeights(weights), []));

        /// <summary>Adds a Fontsource family (weight 400 unless given otherwise), default subsets, <c>font-display: swap</c>, sans-serif fallback.</summary>
        /// <param name="family">UTF-8 CSS family name.</param>
        /// <param name="weights">Numeric weights to fetch; empty fetches weight 400.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FontsOptions AddFontsourceFont(
            ReadOnlySpan<byte> family,
            params int[] weights) =>
            AddFace(options, MakeFace(family.ToArray(), FontProviderKind.Fontsource, NormalizeWeights(weights), []));

        /// <summary>Adds a family backed by local font files matched by <paramref name="src"/> (weight 400, <c>font-display: swap</c>, sans-serif fallback).</summary>
        /// <param name="family">UTF-8 CSS family name.</param>
        /// <param name="src">Glob patterns (relative to the input root) for the font files.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FontsOptions AddLocalFont(
            ReadOnlySpan<byte> family,
            params GlobPattern[] src) =>
            AddFace(options, MakeFace(family.ToArray(), FontProviderKind.Local, [DefaultWeight], src));

        /// <summary>Adds a fully specified face; use this for italic faces, non-preloaded faces, serif/monospace fallbacks, or custom theme variables.</summary>
        /// <param name="face">The face to add.</param>
        /// <returns>The updated options.</returns>
        public FontsOptions AddFace(in FontFace face) =>
            options with { Faces = ArrayJoiner.Concat(options.Faces, [face]) };

        /// <summary>Replaces the offline flag.</summary>
        /// <param name="offline">When true a download-cache miss is an error instead of a fetch.</param>
        /// <returns>The updated options.</returns>
        public FontsOptions WithOffline(bool offline) =>
            options with { Offline = offline };

        /// <summary>Replaces the download-cache directory.</summary>
        /// <param name="cacheDirectory">Directory for the content-addressed cache.</param>
        /// <returns>The updated options.</returns>
        public FontsOptions WithCacheDirectory(in DirectoryPath cacheDirectory) =>
            options with { CacheDirectory = cacheDirectory };

        /// <summary>Replaces the output subdirectory.</summary>
        /// <param name="subdirectory">Site-relative directory (e.g. <c>"assets/fonts"</c>).</param>
        /// <returns>The updated options.</returns>
        public FontsOptions WithOutputSubdirectory(in PathSegment subdirectory) =>
            options with { OutputSubdirectory = subdirectory };

        /// <summary>Empties the declared-face list.</summary>
        /// <returns>The updated options.</returns>
        public FontsOptions ClearFaces() =>
            options with { Faces = [] };

        /// <summary>Replaces the generic fallback family of the most recently added face.</summary>
        /// <param name="fallback">Generic fallback family / reference font.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FontsOptions WithFallback(GenericFontFamily fallback) =>
            ReplaceLast(options, f => f with { Fallback = fallback });

        /// <summary>Sets the most recently added face's <c>font-display</c> descriptor.</summary>
        /// <param name="display">CSS <c>font-display</c> descriptor.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FontsOptions WithDisplay(FontDisplay display) =>
            ReplaceLast(options, f => f with { Display = display });

        /// <summary>Marks the most recently added face as not preloaded.</summary>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FontsOptions WithoutPreload() =>
            ReplaceLast(options, static f => f with { Preload = false });

        /// <summary>Sets the CSS custom properties the most recently added face should drive.</summary>
        /// <param name="themeVariables">UTF-8 names of CSS custom properties (e.g. <c>"--md-text-font"u8</c>).</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FontsOptions WithThemeVariables(params byte[][] themeVariables) =>
            ReplaceLast(options, f => f with { ThemeVariables = themeVariables });

        /// <summary>Replaces the subsets of the most recently added face.</summary>
        /// <param name="subsets">UTF-8 subset names; <c>"all"u8</c> requests every subset the provider offers.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FontsOptions WithSubsets(params byte[][] subsets) =>
            ReplaceLast(options, f => f with { Subsets = subsets is [_, ..] ? subsets : DefaultSubsets() });
    }

    /// <summary>Returns the options with the last declared face replaced by <paramref name="transform"/> applied to it.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="transform">Transformation applied to the last face.</param>
    /// <returns>The updated options (unchanged when there are no faces).</returns>
    private static FontsOptions ReplaceLast(in FontsOptions options, Func<FontFace, FontFace> transform)
    {
        if (options.Faces is not [.., var last])
        {
            return options;
        }

        var faces = (FontFace[])options.Faces.Clone();
        faces[^1] = transform(last);
        return options with { Faces = faces };
    }

    /// <summary>Builds a face with the convenience defaults filled in.</summary>
    /// <param name="family">UTF-8 family name.</param>
    /// <param name="provider">Provider kind.</param>
    /// <param name="weights">Numeric weights.</param>
    /// <param name="localSrc">Glob patterns for a local face; empty for remote providers.</param>
    /// <returns>The constructed face.</returns>
    private static FontFace MakeFace(byte[] family, FontProviderKind provider, int[] weights, GlobPattern[] localSrc) =>
        new(
            DeriveId(family),
            family,
            provider,
            weights,
            [FontStyle.Normal],
            provider is FontProviderKind.Local ? [] : DefaultSubsets(),
            FontDisplay.Swap,
            true,
            GenericFontFamily.SansSerif,
            localSrc,
            []);

    /// <summary>Lowercases ASCII and replaces spaces with hyphens to make a CSS-variable-safe identifier.</summary>
    /// <param name="family">UTF-8 family name.</param>
    /// <returns>The derived identifier bytes.</returns>
    private static byte[] DeriveId(ReadOnlySpan<byte> family)
    {
        var id = new byte[family.Length];
        for (var i = 0; i < family.Length; i++)
        {
            id[i] = family[i] == (byte)' ' ? (byte)'-' : AsciiByteHelpers.ToAsciiLowerByte(family[i]);
        }

        return id;
    }

    /// <summary>Returns <paramref name="weights"/> or <c>[400]</c> when it is empty.</summary>
    /// <param name="weights">Caller-supplied weights.</param>
    /// <returns>A non-empty weight array.</returns>
    private static int[] NormalizeWeights(int[] weights) => weights is [_, ..] ? weights : [DefaultWeight];

    /// <summary>Returns a fresh array of the default subset names (<c>latin</c>, <c>latin-ext</c>).</summary>
    /// <returns>The default subset-name array.</returns>
    private static byte[][] DefaultSubsets() => [[.. "latin"u8], [.. "latin-ext"u8]];
}
