// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// File-system glob pattern (e.g. <c>**/*.md</c>, <c>articles/**</c>) used for include / exclude
/// filters. Distinct from <see cref="DirectoryPath"/> and <see cref="FilePath"/> so that the
/// "this string is a glob, not a path" intent reads from the type at every API boundary.
/// </summary>
/// <param name="Value">The underlying pattern string in
/// <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c> syntax.</param>
public readonly record struct GlobPattern(string Value)
{
    /// <summary>Gets a value indicating whether this pattern is empty (uninitialized / placeholder).</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Implicitly unwraps to a plain <see cref="string"/> for BCL interop with <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c>.</summary>
    /// <param name="pattern">Source pattern.</param>
    public static implicit operator string(GlobPattern pattern) => pattern.Value ?? string.Empty;

    /// <summary>Implicitly wraps a <see cref="string"/> so callers can pass literals to APIs that take a <see cref="GlobPattern"/>.</summary>
    /// <param name="value">Source string; null becomes an empty pattern.</param>
    public static implicit operator GlobPattern(string? value) => new(value ?? string.Empty);

    /// <summary>Friendly named alias for the string→<see cref="GlobPattern"/> implicit operator (CA2225).</summary>
    /// <param name="value">Source string.</param>
    /// <returns>The wrapped pattern.</returns>
    public static GlobPattern FromString(string? value) => value;

    /// <summary>Friendly named alias for the <see cref="GlobPattern"/>→<see cref="string"/> implicit operator (CA2225).</summary>
    /// <param name="pattern">Source pattern.</param>
    /// <returns>The underlying string.</returns>
    public static string ToStringValue(GlobPattern pattern) => pattern;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
