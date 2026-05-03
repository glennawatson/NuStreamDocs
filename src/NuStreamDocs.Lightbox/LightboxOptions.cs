// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Lightbox;

/// <summary>Configuration for <see cref="LightboxPlugin"/>.</summary>
/// <param name="StylesheetUrl">UTF-8 absolute URL to glightbox CSS, emitted directly into the page <c>&lt;head&gt;</c>.</param>
/// <param name="ScriptUrl">UTF-8 absolute URL to glightbox JS, emitted directly into the page <c>&lt;head&gt;</c>.</param>
/// <param name="WrapImages">When true, the plugin rewrites every <c>&lt;img src=&quot;...&quot;&gt;</c> outside an existing anchor into a glightbox anchor wrapper.</param>
/// <param name="Selector">CSS selector glightbox uses to discover targets; default <c>.glightbox</c>.</param>
public sealed record LightboxOptions(byte[] StylesheetUrl, byte[] ScriptUrl, bool WrapImages, string Selector)
{
    /// <summary>Gets the default glightbox jsDelivr CSS pin.</summary>
    public static byte[] DefaultStylesheetUrl { get; } = [.. "https://cdn.jsdelivr.net/npm/glightbox@3.3.1/dist/css/glightbox.min.css"u8];

    /// <summary>Gets the default glightbox jsDelivr JS pin.</summary>
    public static byte[] DefaultScriptUrl { get; } = [.. "https://cdn.jsdelivr.net/npm/glightbox@3.3.1/dist/js/glightbox.min.js"u8];

    /// <summary>Gets the default selector applied to lightbox-wrapped anchors.</summary>
    public static string DefaultSelector => "glightbox";

    /// <summary>Gets the default options: CDN URLs, image-wrapping enabled, default selector.</summary>
    public static LightboxOptions Default => new(DefaultStylesheetUrl, DefaultScriptUrl, WrapImages: true, DefaultSelector);
}
