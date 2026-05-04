// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Marker wrapper for the narrow set of public-API surfaces that must accept or return a
/// <see cref="string"/> for outside-consumer compatibility. Implicitly converts to and from
/// <see cref="string"/> so call sites stay trivial; the type itself signals that the string
/// lives here only because an external contract demands it, and should be eliminated the
/// moment that contract changes.
/// </summary>
/// <remarks>
/// Production code in this repository is byte-first: text moves as
/// <see cref="ReadOnlySpan{Byte}"/> / <see cref="byte"/> arrays through the parse / render / emit
/// pipeline, and path-shaped APIs use <see cref="FilePath"/> / <see cref="DirectoryPath"/> /
/// <see cref="PathSegment"/> / <see cref="GlobPattern"/> / <see cref="UrlPath"/>. <see cref="string"/>
/// has no place in new code — including private helpers. Reach for <see cref="ApiCompatString"/>
/// only at the seam where a third-party consumer (config-file value, BCL signature we cannot
/// override, plugin host that hands us text) forces a <see cref="string"/> across the boundary.
/// Encode it once at construction so the rest of the call graph keeps its byte-shaped invariants.
/// </remarks>
/// <param name="Value">The compatibility string. May be <see langword="null"/>; reads surface as
/// <see cref="string.Empty"/>.</param>
public readonly record struct ApiCompatString(string? Value)
{
    /// <summary>Gets a value indicating whether the wrapped string is null or empty.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Implicitly unwraps to <see cref="string"/> so BCL / external APIs consume the wrapper directly.</summary>
    /// <param name="value">The wrapper.</param>
    /// <returns>The underlying string, or <see cref="string.Empty"/> when the wrapper is default / null.</returns>
    public static implicit operator string(ApiCompatString value) => value.Value ?? string.Empty;

    /// <summary>Implicitly wraps a <see cref="string"/> coming from an outside consumer.</summary>
    /// <param name="value">External string.</param>
    /// <returns>A wrapper carrying <paramref name="value"/>.</returns>
    public static implicit operator ApiCompatString(string? value) => new(value);

    /// <summary>Friendly named alias for the <see cref="string"/>→<see cref="ApiCompatString"/> implicit operator (CA2225).</summary>
    /// <param name="value">External string.</param>
    /// <returns>A wrapper carrying <paramref name="value"/>.</returns>
    public static ApiCompatString FromString(string? value) => new(value);

    /// <summary>Friendly named alias for the <see cref="ApiCompatString"/>→<see cref="string"/> implicit operator (CA2225).</summary>
    /// <returns>The underlying string, or <see cref="string.Empty"/> when the wrapper is default / null.</returns>
    public string ToStringValue() => Value ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
