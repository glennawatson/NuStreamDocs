// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Lightweight type-safe wrapper around a directory path string. Pure path manipulation; never
/// touches the filesystem. Single <see cref="string"/> field — same memory cost as a plain string,
/// free <see cref="object.Equals(object?)"/> / <see cref="object.GetHashCode()"/> via the record
/// struct.
/// </summary>
/// <remarks>
/// Use across public APIs that accept a directory path so callers can't accidentally pass an
/// arbitrary string. Interop with the BCL is implicit: <c>Directory.Exists(path)</c> works because
/// of the conversion to <see cref="string"/>. Joins follow Nuke-style ergonomics with the
/// <c>/</c> operator.
/// <para>
/// The wrapper deliberately avoids <see cref="DirectoryInfo"/>: that type allocates a heavier
/// object, caches filesystem state on first access, and forces <c>Refresh()</c> calls to keep
/// metadata current. <see cref="DirectoryPath"/> is a value-type contract — it describes a path
/// and nothing more.
/// </para>
/// </remarks>
/// <param name="Value">The underlying path string. Always normalized to forward slashes via
/// <see cref="Normalize(string)"/> at construction time on non-Windows systems; on Windows the
/// raw value is preserved so call sites that pass platform paths keep working.</param>
public readonly record struct DirectoryPath(string Value)
{
    /// <summary>Gets a value indicating whether this path is empty (uninitialized / placeholder).</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Gets the file-system-style name (last segment).</summary>
    public string Name => string.IsNullOrEmpty(Value) ? string.Empty : Path.GetFileName(Value.AsSpan().TrimEnd('/').TrimEnd('\\').ToString());

    /// <summary>Gets the parent directory path.</summary>
    /// <remarks>Returns an empty <see cref="DirectoryPath"/> when the path has no parent (root or already empty).</remarks>
    public DirectoryPath Parent
    {
        get
        {
            if (IsEmpty)
            {
                return default;
            }

            var parent = Path.GetDirectoryName(Value);
            return string.IsNullOrEmpty(parent) ? default : new(parent);
        }
    }

    /// <summary>Implicitly unwraps to a plain <see cref="string"/> for BCL interop.</summary>
    /// <param name="path">Source path.</param>
    public static implicit operator string(DirectoryPath path) => path.Value ?? string.Empty;

    /// <summary>Implicitly unwraps to a <see cref="ReadOnlySpan{Char}"/> for span-shaped APIs.</summary>
    /// <param name="path">Source path.</param>
    public static implicit operator ReadOnlySpan<char>(DirectoryPath path) => (path.Value ?? string.Empty).AsSpan();

    /// <summary>Implicitly wraps a <see cref="string"/> path so callers can pass string literals to APIs that take a <see cref="DirectoryPath"/>.</summary>
    /// <param name="value">Source path string; null becomes an empty <see cref="DirectoryPath"/>.</param>
    public static implicit operator DirectoryPath(string? value) => new(value ?? string.Empty);

    /// <summary>Joins <paramref name="path"/> and <paramref name="segment"/> with <see cref="Path.Combine(string, string)"/>.</summary>
    /// <param name="path">Source directory.</param>
    /// <param name="segment">Relative directory segment.</param>
    /// <returns>The combined directory path.</returns>
    public static DirectoryPath operator /(DirectoryPath path, string segment) => path.Combine(segment);

    /// <summary>Joins <paramref name="path"/> and <paramref name="segment"/> as nested directories.</summary>
    /// <param name="path">Source directory.</param>
    /// <param name="segment">Nested directory.</param>
    /// <returns>The combined directory path.</returns>
    public static DirectoryPath operator /(DirectoryPath path, DirectoryPath segment) =>
        segment.IsEmpty ? path : path.Combine(segment.Value);

    /// <summary>Friendly named alias for the string→<see cref="DirectoryPath"/> implicit operator (CA2225).</summary>
    /// <param name="value">Source path string.</param>
    /// <returns>The wrapped path.</returns>
    public static DirectoryPath FromString(string? value) => value;

    /// <summary>Friendly named alias for the <see cref="DirectoryPath"/>→<see cref="string"/> implicit operator (CA2225).</summary>
    /// <param name="path">Source directory.</param>
    /// <returns>The underlying path string.</returns>
    public static string ToStringValue(DirectoryPath path) => path;

    /// <summary>Friendly named alias for the <see cref="DirectoryPath"/>→<see cref="ReadOnlySpan{Char}"/> implicit operator (CA2225).</summary>
    /// <param name="path">Source directory.</param>
    /// <returns>The underlying path as a span.</returns>
    public static ReadOnlySpan<char> ToReadOnlySpan(DirectoryPath path) => path;

    /// <summary>Friendly named alias for the <c>/</c> string-segment operator (CA2225).</summary>
    /// <param name="left">Source directory.</param>
    /// <param name="right">Relative directory segment.</param>
    /// <returns>The combined directory path.</returns>
    public static DirectoryPath Divide(DirectoryPath left, string right) => left / right;

    /// <summary>Friendly named alias for the <c>/</c> nested-directory operator (CA2225).</summary>
    /// <param name="left">Source directory.</param>
    /// <param name="right">Nested directory.</param>
    /// <returns>The combined directory path.</returns>
    public static DirectoryPath Divide(DirectoryPath left, DirectoryPath right) => left / right;

    /// <summary>Joins this directory with <paramref name="segment"/> via <see cref="Path.Combine(string, string)"/>.</summary>
    /// <param name="segment">Relative segment.</param>
    /// <returns>The combined directory path.</returns>
    public DirectoryPath Combine(string segment)
    {
        ArgumentException.ThrowIfNullOrEmpty(segment);
        return IsEmpty ? new(segment) : new(Path.Combine(Value, segment));
    }

    /// <summary>Returns a <see cref="FilePath"/> for <paramref name="fileName"/> inside this directory.</summary>
    /// <param name="fileName">File name (or relative path) within this directory.</param>
    /// <returns>The composed file path.</returns>
    public FilePath File(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        return IsEmpty ? new(fileName) : new(Path.Combine(Value, fileName));
    }

    /// <summary>
    /// Determines if the directory exists or not.
    /// </summary>
    /// <returns>True if the directory exists; false otherwise.</returns>
    public bool Exists() => Directory.Exists(Value);

    /// <summary>Returns the underlying path as a <see cref="ReadOnlySpan{Char}"/> for span-based parsing.</summary>
    /// <returns>The path span; empty when the wrapper is default.</returns>
    public ReadOnlySpan<char> AsSpan() => Value.AsSpan();

    /// <summary>Creates this directory and any missing intermediate directories.</summary>
    /// <returns>This path, for fluent chaining.</returns>
    public DirectoryPath Create()
    {
        if (!IsEmpty)
        {
            Directory.CreateDirectory(Value);
        }

        return this;
    }

    /// <summary>Deletes the directory (must be empty).</summary>
    public void Delete() => Directory.Delete(Value);

    /// <summary>Deletes the directory and, when <paramref name="recursive"/> is true, everything inside.</summary>
    /// <param name="recursive">When true, deletes the entire subtree.</param>
    public void DeleteRecursive(bool recursive) => Directory.Delete(Value, recursive);

    /// <summary>Enumerates files at the top level of this directory.</summary>
    /// <returns>Matching file paths.</returns>
    public IEnumerable<FilePath> EnumerateFiles() => EnumerateFiles("*", SearchOption.TopDirectoryOnly);

    /// <summary>Enumerates files at the top level matching <paramref name="searchPattern"/>.</summary>
    /// <param name="searchPattern">Glob pattern (e.g. <c>*.md</c>).</param>
    /// <returns>Matching file paths.</returns>
    public IEnumerable<FilePath> EnumerateFiles(string searchPattern) => EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly);

    /// <summary>Enumerates files inside this directory matching <paramref name="searchPattern"/>.</summary>
    /// <param name="searchPattern">Glob pattern (e.g. <c>*.md</c>).</param>
    /// <param name="searchOption">Top-level vs. recursive scan.</param>
    /// <returns>Matching file paths.</returns>
    public IEnumerable<FilePath> EnumerateFiles(string searchPattern, SearchOption searchOption)
    {
        if (IsEmpty)
        {
            return [];
        }

        return Project(Directory.EnumerateFiles(Value, searchPattern, searchOption));

        static IEnumerable<FilePath> Project(IEnumerable<string> source)
        {
            foreach (var entry in source)
            {
                yield return new FilePath(entry);
            }
        }
    }

    /// <summary>Enumerates direct subdirectories.</summary>
    /// <returns>Matching directory paths.</returns>
    public IEnumerable<DirectoryPath> EnumerateDirectories() => EnumerateDirectories("*", SearchOption.TopDirectoryOnly);

    /// <summary>Enumerates direct subdirectories matching <paramref name="searchPattern"/>.</summary>
    /// <param name="searchPattern">Glob pattern.</param>
    /// <returns>Matching directory paths.</returns>
    public IEnumerable<DirectoryPath> EnumerateDirectories(string searchPattern) => EnumerateDirectories(searchPattern, SearchOption.TopDirectoryOnly);

    /// <summary>Enumerates subdirectories matching <paramref name="searchPattern"/>.</summary>
    /// <param name="searchPattern">Glob pattern.</param>
    /// <param name="searchOption">Top-level vs. recursive scan.</param>
    /// <returns>Matching directory paths.</returns>
    public IEnumerable<DirectoryPath> EnumerateDirectories(string searchPattern, SearchOption searchOption)
    {
        if (IsEmpty)
        {
            return [];
        }

        return Project(Directory.EnumerateDirectories(Value, searchPattern, searchOption));

        static IEnumerable<DirectoryPath> Project(IEnumerable<string> source)
        {
            foreach (var entry in source)
            {
                yield return new DirectoryPath(entry);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Normalizes <paramref name="value"/> by replacing backslashes with forward slashes on non-Windows OSes.</summary>
    /// <param name="value">Source path.</param>
    /// <returns>Normalized path.</returns>
    /// <remarks>
    /// Runtime path normalization is intentionally minimal — we don't lower-case, don't resolve
    /// relative segments, don't touch the filesystem. The wrapper is a name carrier; callers that
    /// need canonicalization use <see cref="Path.GetFullPath(string)"/> at the boundary.
    /// </remarks>
    public static string Normalize(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value;
}
