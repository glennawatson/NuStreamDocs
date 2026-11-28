// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Social;

/// <summary>Configuration for <see cref="SocialPlugin"/>.</summary>
[System.Diagnostics.DebuggerDisplay("SocialOptions: Enabled={Enabled}, SvgCards={GenerateSvgCards}")]
public readonly record struct SocialOptions()
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static SocialOptions Default { get; } = new();

    /// <summary>Gets a value indicating whether the plugin emits head tags and social cards.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets a value indicating whether OpenGraph (<c>og:*</c>) meta tags are emitted.</summary>
    public bool EmitOpenGraph { get; init; } = true;

    /// <summary>Gets a value indicating whether Twitter/X (<c>twitter:*</c>) meta tags are emitted.</summary>
    public bool EmitTwitterCard { get; init; } = true;

    /// <summary>Gets a value indicating whether Schema.org <c>TechArticle</c> JSON-LD is emitted.</summary>
    public bool EmitJsonLd { get; init; } = true;

    /// <summary>Gets a value indicating whether a <c>&lt;link rel="canonical"&gt;</c> tag is emitted when <c>site_url</c> is configured and none is already present.</summary>
    public bool EmitCanonicalLink { get; init; } = true;

    /// <summary>Gets a value indicating whether deterministic SVG preview cards are written to <c>assets/social/&lt;slug&gt;.svg</c> for pages without an explicit image override.</summary>
    public bool GenerateSvgCards { get; init; } = true;

    /// <summary>Gets the optional UTF-8 Twitter/X account handle (for example <c>"@myproject"u8</c>) emitted as <c>twitter:site</c>.</summary>
    public byte[] TwitterSite { get; init; } = [];

    /// <summary>Gets the UTF-8 CSS color fill used for accent highlights on generated SVG cards.</summary>
    public byte[] AccentColor { get; init; } = [.. DefaultAccentColor];

    /// <summary>Gets the UTF-8 CSS color fill used for the background of generated SVG cards.</summary>
    public byte[] BackgroundColor { get; init; } = [.. DefaultBackgroundColor];

    /// <summary>Gets the default blue accent color bytes.</summary>
    private static ReadOnlySpan<byte> DefaultAccentColor => "#2563eb"u8;

    /// <summary>Gets the default slate background color bytes.</summary>
    private static ReadOnlySpan<byte> DefaultBackgroundColor => "#0f172a"u8;
}
