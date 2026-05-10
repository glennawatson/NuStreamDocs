// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Marker wrapper for diagnostic messages that must surface as a <see cref="string"/> because
/// the consuming sink — <c>ILogger</c>, <see cref="Exception.Message"/>, browser-displayed
/// HTML — demands one. Use of this type at an API boundary signals "we deliberately leak a
/// <see cref="string"/> here only because the encoding step is unavoidable for human-readable
/// output", complementing <see cref="ApiCompatString"/> (BCL/consumer interop).
/// </summary>
/// <param name="Value">The wrapped diagnostic text. May be <see langword="null"/>; reads surface as <see cref="string.Empty"/>.</param>
public readonly record struct DiagnosticMessage(string? Value)
{
    /// <summary>Gets the empty diagnostic message (used for "no diagnostic" returns paired with a non-nullable struct).</summary>
    public static DiagnosticMessage None => default;

    /// <summary>Gets a value indicating whether the wrapped message is null or empty.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Implicitly unwraps to <see cref="string"/> for sinks that demand one.</summary>
    /// <param name="value">The wrapper.</param>
    /// <returns>The underlying message, or <see cref="string.Empty"/> when null.</returns>
    public static implicit operator string(DiagnosticMessage value) => value.Value ?? string.Empty;

    /// <summary>Implicitly wraps a <see cref="string"/> from an interpolated diagnostic site.</summary>
    /// <param name="value">Composed diagnostic text.</param>
    /// <returns>A wrapper carrying <paramref name="value"/>.</returns>
    public static implicit operator DiagnosticMessage(string? value) => new(value);

    /// <summary>Named alias for the <see cref="string"/> to <see cref="DiagnosticMessage"/> implicit operator.</summary>
    /// <param name="value">Composed diagnostic text.</param>
    /// <returns>A wrapper carrying <paramref name="value"/>.</returns>
    public static DiagnosticMessage FromString(string? value) => new(value);

    /// <summary>Named alias for the <see cref="DiagnosticMessage"/> to <see cref="string"/> implicit operator.</summary>
    /// <returns>The underlying message, or <see cref="string.Empty"/> when null.</returns>
    public string ToStringValue() => Value ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
