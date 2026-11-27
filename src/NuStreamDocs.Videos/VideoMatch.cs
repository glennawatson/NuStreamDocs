// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Videos;

/// <summary>Zero-allocation slices describing a matched video URL inside the source buffer.</summary>
[System.Diagnostics.DebuggerDisplay("VideoMatch: {Kind}")]
internal readonly ref struct VideoMatch
{
    /// <summary>Initializes a new instance of the <see cref="VideoMatch"/> struct.</summary>
    /// <param name="kind">Matched concrete provider kind.</param>
    /// <param name="primary">Primary video identifier, URL prefix, or full URL slice.</param>
    /// <param name="secondary">Optional secondary identifier (unlisted hash, account ID, customer host, or MIME subtype).</param>
    /// <param name="query">Optional query slice without leading <c>?</c>.</param>
    /// <param name="fragment">Optional fragment slice without leading <c>#</c>.</param>
    /// <param name="startSeconds">Parsed start offset in whole seconds, or <c>-1</c> when absent.</param>
    internal VideoMatch(
        VideoProviderKind kind,
        ReadOnlySpan<byte> primary,
        ReadOnlySpan<byte> secondary,
        ReadOnlySpan<byte> query,
        ReadOnlySpan<byte> fragment,
        int startSeconds)
    {
        Kind = kind;
        Primary = primary;
        Secondary = secondary;
        Query = query;
        Fragment = fragment;
        StartSeconds = startSeconds;
    }

    /// <summary>Gets the matched provider kind.</summary>
    internal VideoProviderKind Kind { get; }

    /// <summary>Gets the primary identifier, base URL prefix, or full URL slice.</summary>
    internal ReadOnlySpan<byte> Primary { get; }

    /// <summary>Gets the secondary identifier (Vimeo hash, Azure account ID, Cloudflare customer host, or HTML5 MIME type).</summary>
    internal ReadOnlySpan<byte> Secondary { get; }

    /// <summary>Gets the query string slice (without leading <c>?</c>).</summary>
    internal ReadOnlySpan<byte> Query { get; }

    /// <summary>Gets the URL fragment slice (without leading <c>#</c>).</summary>
    internal ReadOnlySpan<byte> Fragment { get; }

    /// <summary>Gets the parsed start timestamp in seconds, or <c>-1</c> when none was specified.</summary>
    internal int StartSeconds { get; }
}
