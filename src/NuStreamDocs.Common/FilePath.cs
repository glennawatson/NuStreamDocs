// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Lightweight type-safe wrapper around a file path string. Pure path manipulation; never touches
/// the filesystem. Single <see cref="string"/> field — same memory cost as a plain string, free
/// <see cref="object.Equals(object?)"/> / <see cref="object.GetHashCode()"/> via the record struct.
/// </summary>
/// <remarks>
/// Sister to <see cref="DirectoryPath"/>. Use across public APIs that take a path to a single file
/// (page source, asset, manifest, etc.). BCL interop is implicit through the conversion to
/// <see cref="string"/>; methods like <see cref="File"/>'s <c>OpenRead</c> work directly.
/// </remarks>
/// <param name="Value">The underlying path string. The wrapper does not normalize separators —
/// callers that need canonicalization use <see cref="Path.GetFullPath(string)"/> at the boundary.</param>
public readonly record struct FilePath(string Value)
{
    /// <summary>Gets a value indicating whether this path is empty (uninitialized / placeholder).</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Gets the file name (last segment, including any extension).</summary>
    public string FileName => string.IsNullOrEmpty(Value) ? string.Empty : Path.GetFileName(Value);

    /// <summary>Gets the file name without its extension.</summary>
    public string FileNameWithoutExtension =>
        string.IsNullOrEmpty(Value) ? string.Empty : Path.GetFileNameWithoutExtension(Value);

    /// <summary>Gets the file extension (including the leading dot, or empty when none).</summary>
    public string Extension =>
        string.IsNullOrEmpty(Value) ? string.Empty : Path.GetExtension(Value);

    /// <summary>Gets the parent directory path.</summary>
    /// <remarks>Returns an empty <see cref="DirectoryPath"/> when the file has no directory component.</remarks>
    public DirectoryPath Directory
    {
        get
        {
            if (IsEmpty)
            {
                return default;
            }

            var dir = Path.GetDirectoryName(Value);
            return string.IsNullOrEmpty(dir) ? default : new(dir);
        }
    }

    /// <summary>Implicitly unwraps to a plain <see cref="string"/> for BCL interop.</summary>
    /// <param name="path">Source file path.</param>
    public static implicit operator string(FilePath path) => path.Value ?? string.Empty;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Returns a copy with the extension swapped to <paramref name="extension"/>.</summary>
    /// <param name="extension">New extension; should include the leading dot.</param>
    /// <returns>The renamed file path.</returns>
    public FilePath WithExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return IsEmpty ? this : new(Path.ChangeExtension(Value, extension));
    }
}
