// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Images;

/// <summary>Configuration for <see cref="ImagesPlugin"/>.</summary>
[System.Diagnostics.DebuggerDisplay("ImagesOptions: Enabled={Enabled}, Dimensions={InjectDimensions}")]
public readonly record struct ImagesOptions()
{
    /// <summary>Gets the default option set.</summary>
    public static ImagesOptions Default { get; } = new();

    /// <summary>Gets a value indicating whether image rewriting and stylesheet injection are active.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets a value indicating whether missing <c>width</c> and <c>height</c> attributes are populated from local image headers.</summary>
    public bool InjectDimensions { get; init; } = true;

    /// <summary>Gets a value indicating whether <c>loading="lazy"</c> is added to images without an explicit <c>loading</c> attribute.</summary>
    public bool LazyLoading { get; init; } = true;

    /// <summary>Gets a value indicating whether <c>decoding="async"</c> is added to images without an explicit <c>decoding</c> attribute.</summary>
    public bool AsyncDecoding { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether images with a non-empty <c>title</c> attribute outside existing
    /// <c>&lt;figure&gt;</c> blocks are wrapped in <c>&lt;figure&gt;</c> and <c>&lt;figcaption&gt;</c>.
    /// </summary>
    public bool WrapTitledInFigure { get; init; } = true;

    /// <summary>Gets the number of leading images on each page that receive <c>loading="eager"</c> instead of <c>loading="lazy"</c>.</summary>
    public int EagerFirstCount { get; init; }
}
