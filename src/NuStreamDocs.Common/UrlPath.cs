// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// URL path component (e.g. <c>/assets/stylesheets/highlight.css</c>, <c>https://example.com/foo/</c>)
/// distinct from a filesystem path. Pure string carrier; never touches the filesystem and never
/// tries to resolve a base URL — composition is up to the caller. Distinct from
/// <see cref="DirectoryPath"/> / <see cref="FilePath"/> so the "this is a URL, not a disk path"
/// intent reads from the type at every API boundary, even though both shapes happen to round-trip
/// through forward-slashed strings.
/// </summary>
/// <param name="Value">The underlying URL string. Forward slashes preserved; no normalization.</param>
public readonly record struct UrlPath(string Value)
{
    /// <summary>Gets a value indicating whether this URL is empty (uninitialized / placeholder).</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Gets a value indicating whether the URL is absolute (has an http(s) scheme or starts with <c>//</c>).</summary>
    public bool IsAbsolute =>
        !string.IsNullOrEmpty(Value)
        && (Value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || Value.StartsWith("//", StringComparison.Ordinal));

    /// <summary>Implicitly unwraps to a plain <see cref="string"/> for HTML-attribute / JSON / logger interop.</summary>
    /// <param name="url">Source URL.</param>
    public static implicit operator string(UrlPath url) => url.Value ?? string.Empty;

    /// <summary>Implicitly wraps a <see cref="string"/> so callers can pass URL literals to APIs that take a <see cref="UrlPath"/>.</summary>
    /// <param name="value">Source URL string; null becomes an empty <see cref="UrlPath"/>.</param>
    public static implicit operator UrlPath(string? value) => new(value ?? string.Empty);

    /// <summary>Friendly named alias for the string→<see cref="UrlPath"/> implicit operator (CA2225).</summary>
    /// <param name="value">Source URL string.</param>
    /// <returns>The wrapped URL.</returns>
    public static UrlPath FromString(string? value) => value;

    /// <summary>Friendly named alias for the <see cref="UrlPath"/>→<see cref="string"/> implicit operator (CA2225).</summary>
    /// <param name="url">Source URL.</param>
    /// <returns>The underlying URL string.</returns>
    public static string ToStringValue(UrlPath url) => url;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
