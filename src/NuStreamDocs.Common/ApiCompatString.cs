// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Marker wrapper for public-API surfaces that must accept or return a <see cref="string"/> for
/// outside-consumer compatibility. Implicitly converts to and from <see cref="string"/>.
/// </summary>
/// <param name="Value">The compatibility string. May be <see langword="null"/>; reads surface as <see cref="string.Empty"/>.</param>
public readonly record struct ApiCompatString(string? Value)
{
    /// <summary>Gets a value indicating whether the wrapped string is null or empty.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Implicitly unwraps to <see cref="string"/>.</summary>
    /// <param name="value">The wrapper.</param>
    /// <returns>The underlying string, or <see cref="string.Empty"/> when null.</returns>
    public static implicit operator string(in ApiCompatString value) => value.Value ?? string.Empty;

    /// <summary>Implicitly wraps a <see cref="string"/>.</summary>
    /// <param name="value">External string.</param>
    /// <returns>A wrapper carrying <paramref name="value"/>.</returns>
    public static implicit operator ApiCompatString(string? value) => new(value);

    /// <summary>Named alias for the <see cref="string"/> to <see cref="ApiCompatString"/> implicit operator.</summary>
    /// <param name="value">External string.</param>
    /// <returns>A wrapper carrying <paramref name="value"/>.</returns>
    public static ApiCompatString FromString(string? value) => new(value);

    /// <summary>Named alias for the <see cref="ApiCompatString"/> to <see cref="string"/> implicit operator.</summary>
    /// <returns>The underlying string, or <see cref="string.Empty"/> when null.</returns>
    public string ToStringValue() => Value ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
