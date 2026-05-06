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

    /// <summary>Implicitly unwraps to a <see cref="ReadOnlySpan{Char}"/> for span-shaped APIs.</summary>
    /// <param name="path">Source file path.</param>
    public static implicit operator ReadOnlySpan<char>(FilePath path) => (path.Value ?? string.Empty).AsSpan();

    /// <summary>Implicitly wraps a <see cref="string"/> path so callers can pass string literals to APIs that take a <see cref="FilePath"/>.</summary>
    /// <param name="value">Source path string; null becomes an empty <see cref="FilePath"/>.</param>
    public static implicit operator FilePath(string? value) => new(value ?? string.Empty);

    /// <summary>Friendly named alias for the string→<see cref="FilePath"/> implicit operator (CA2225).</summary>
    /// <param name="value">Source path string.</param>
    /// <returns>The wrapped path.</returns>
    public static FilePath FromString(string? value) => value;

    /// <summary>Friendly named alias for the <see cref="FilePath"/>→<see cref="string"/> implicit operator (CA2225).</summary>
    /// <param name="path">Source file.</param>
    /// <returns>The underlying path string.</returns>
    public static string ToStringValue(FilePath path) => path;

    /// <summary>Friendly named alias for the <see cref="FilePath"/>→<see cref="ReadOnlySpan{Char}"/> implicit operator (CA2225).</summary>
    /// <param name="path">Source file.</param>
    /// <returns>The underlying path as a span.</returns>
    public static ReadOnlySpan<char> ToReadOnlySpan(FilePath path) => path;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Returns the underlying path as a <see cref="ReadOnlySpan{Char}"/> for span-based parsing.</summary>
    /// <returns>The path span; empty when the wrapper is default.</returns>
    public ReadOnlySpan<char> AsSpan() => Value.AsSpan();

    /// <summary>Returns true when this path ends with <paramref name="value"/> ordinally.</summary>
    /// <param name="value">Suffix to test for.</param>
    /// <returns>True when the path ends with <paramref name="value"/>.</returns>
    public bool EndsWith(ReadOnlySpan<char> value) =>
        AsSpan().EndsWith(value, StringComparison.Ordinal);

    /// <summary>Returns true when this path ends with <paramref name="value"/> using the supplied comparison.</summary>
    /// <param name="value">Suffix to test for.</param>
    /// <param name="comparison">Comparison kind.</param>
    /// <returns>True when the path ends with <paramref name="value"/>.</returns>
    public bool EndsWith(ReadOnlySpan<char> value, StringComparison comparison) =>
        AsSpan().EndsWith(value, comparison);

    /// <summary>Returns a copy with every <paramref name="oldChar"/> replaced by <paramref name="newChar"/> — typically used to normalize <c>\</c> to <c>/</c>.</summary>
    /// <param name="oldChar">Character to find.</param>
    /// <param name="newChar">Character to substitute.</param>
    /// <returns>The rewritten path.</returns>
    public FilePath Replace(char oldChar, char newChar) =>
        IsEmpty ? this : new(Value.Replace(oldChar, newChar));

    /// <summary>Returns a copy with the extension swapped to <paramref name="extension"/>.</summary>
    /// <param name="extension">New extension; should include the leading dot.</param>
    /// <returns>The renamed file path.</returns>
    public FilePath WithExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return IsEmpty ? this : new(Path.ChangeExtension(Value, extension));
    }

    /// <summary>Determines whether this file currently exists on disk.</summary>
    /// <returns>True when the file exists; otherwise false.</returns>
    public bool Exists() => File.Exists(Value);

    /// <summary>Reads the file's contents as UTF-8 bytes, stripping any leading BOM.</summary>
    /// <returns>The file bytes.</returns>
    public byte[] ReadAllBytes() => Utf8Bom.StripIfPresent(File.ReadAllBytes(Value));

    /// <summary>Asynchronously reads the file's contents as UTF-8 bytes, stripping any leading BOM.</summary>
    /// <returns>The file bytes.</returns>
    public async Task<byte[]> ReadAllBytesAsync() =>
        Utf8Bom.StripIfPresent(await File.ReadAllBytesAsync(Value).ConfigureAwait(false));

    /// <summary>Asynchronously reads the file's contents as UTF-8 bytes, stripping any leading BOM.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file bytes.</returns>
    public async Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken) =>
        Utf8Bom.StripIfPresent(await File.ReadAllBytesAsync(Value, cancellationToken).ConfigureAwait(false));

    /// <summary>Writes <paramref name="bytes"/> to this file, creating or overwriting it.</summary>
    /// <param name="bytes">Source bytes.</param>
    public void WriteAllBytes(ReadOnlySpan<byte> bytes) => File.WriteAllBytes(Value, bytes);

    /// <summary>Asynchronously writes <paramref name="bytes"/> to this file, creating or overwriting it.</summary>
    /// <param name="bytes">Source bytes.</param>
    /// <returns>A task that completes when the write finishes.</returns>
    public Task WriteAllBytesAsync(ReadOnlyMemory<byte> bytes) =>
        File.WriteAllBytesAsync(Value, bytes);

    /// <summary>Asynchronously writes <paramref name="bytes"/> to this file, creating or overwriting it.</summary>
    /// <param name="bytes">Source bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the write finishes.</returns>
    public Task WriteAllBytesAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        File.WriteAllBytesAsync(Value, bytes, cancellationToken);

    /// <summary>Opens the file for reading.</summary>
    /// <returns>A read-only stream over the file contents.</returns>
    public FileStream OpenRead() => File.OpenRead(Value);

    /// <summary>Creates or truncates the file and opens it for writing.</summary>
    /// <returns>A writable stream.</returns>
    public FileStream Create() => File.Create(Value);

    /// <summary>Deletes the file if it exists.</summary>
    public void Delete() => File.Delete(Value);
}
