// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Relative path segment (e.g. <c>api</c>, <c>blog/posts</c>) used for subdirectory references
/// resolved against a root <see cref="DirectoryPath"/> at runtime. Pure name carrier; never touches
/// the filesystem. Distinct from <see cref="DirectoryPath"/> so that absolute-vs-relative intent
/// reads from the type at every API boundary.
/// </summary>
/// <param name="Value">The underlying segment string. May contain forward slashes (multi-level)
/// but never represents an absolute path.</param>
public readonly record struct PathSegment(string Value)
{
    /// <summary>Gets a value indicating whether this segment is empty (uninitialized / placeholder).</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Implicitly unwraps to a plain <see cref="string"/> for BCL interop.</summary>
    /// <param name="segment">Source segment.</param>
    public static implicit operator string(PathSegment segment) => segment.Value ?? string.Empty;

    /// <summary>Implicitly wraps a <see cref="string"/> so callers can pass literals to APIs that take a <see cref="PathSegment"/>.</summary>
    /// <param name="value">Source string; null becomes an empty segment.</param>
    public static implicit operator PathSegment(string? value) => new(value ?? string.Empty);

    /// <summary>Friendly named alias for the string→<see cref="PathSegment"/> implicit operator (CA2225).</summary>
    /// <param name="value">Source string.</param>
    /// <returns>The wrapped segment.</returns>
    public static PathSegment FromString(string? value) => value;

    /// <summary>Friendly named alias for the <see cref="PathSegment"/>→<see cref="string"/> implicit operator (CA2225).</summary>
    /// <param name="segment">Source segment.</param>
    /// <returns>The underlying string.</returns>
    public static string ToStringValue(PathSegment segment) => segment;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
