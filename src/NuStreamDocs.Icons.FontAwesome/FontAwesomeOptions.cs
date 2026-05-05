// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Icons.FontAwesome;

/// <summary>Configuration for <see cref="FontAwesomePlugin"/>.</summary>
/// <param name="StylesheetUrl">UTF-8 URL of the Font Awesome stylesheet to inject into <c>&lt;head&gt;</c>.</param>
/// <param name="Crossorigin">Optional UTF-8 <c>crossorigin</c> attribute value; empty to omit.</param>
/// <param name="ReferrerPolicy">Optional UTF-8 <c>referrerpolicy</c> attribute value; empty to omit.</param>
public readonly record struct FontAwesomeOptions(
    byte[] StylesheetUrl,
    byte[] Crossorigin,
    byte[] ReferrerPolicy)
{
    /// <summary>Gets the default Font Awesome Free CDN URL (CSS, all variants).</summary>
    /// <remarks>
    /// Pinned to a specific Font Awesome Free release so the layout
    /// doesn't drift when upstream rev-bumps. Bump in lockstep when
    /// content authors need newer icon glyphs.
    /// </remarks>
    public static byte[] DefaultStylesheetUrl { get; } =
        [.. "https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.5.2/css/all.min.css"u8];

    /// <summary>Gets the default <c>crossorigin</c> attribute value.</summary>
    public static byte[] DefaultCrossorigin { get; } = [.. "anonymous"u8];

    /// <summary>Gets the default <c>referrerpolicy</c> attribute value.</summary>
    public static byte[] DefaultReferrerPolicy { get; } = [.. "no-referrer"u8];

    /// <summary>Gets the option set with all defaults populated.</summary>
    public static FontAwesomeOptions Default { get; } = new(
        StylesheetUrl: DefaultStylesheetUrl,
        Crossorigin: DefaultCrossorigin,
        ReferrerPolicy: DefaultReferrerPolicy);
}
