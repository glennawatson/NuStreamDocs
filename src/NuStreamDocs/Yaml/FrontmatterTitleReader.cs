// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Yaml;

/// <summary>
/// Reads a page's front-matter <c>title:</c> without parsing the whole document.
/// </summary>
/// <remarks>
/// The nav builders only need a lightweight title peek while walking the docs tree.
/// This helper keeps that logic shared between curated and auto-discovered nav paths.
/// </remarks>
public static class FrontmatterTitleReader
{
    /// <summary>Maximum bytes read from the head of each file.</summary>
    private const int FrontmatterPeekBytes = 1024;

    /// <summary>Returns the front-matter <c>title:</c> for <paramref name="absolutePath"/> when present.</summary>
    /// <param name="absolutePath">Absolute path to a markdown page.</param>
    /// <returns>The decoded title, or <see langword="null"/> when absent or unreadable.</returns>
    public static string? Read(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(absolutePath);
        var bytes = ReadTitleBytes(absolutePath);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Returns the front-matter <c>title:</c> bytes for <paramref name="absolutePath"/> when present.</summary>
    /// <param name="absolutePath">Absolute path to a markdown page.</param>
    /// <returns>The UTF-8 title bytes, or <see langword="null"/> when absent or unreadable.</returns>
    public static byte[]? ReadBytes(FilePath absolutePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(absolutePath.Value);
        return ReadTitleBytes(absolutePath.Value);
    }

    /// <summary>Reads the head of <paramref name="absolutePath"/> and returns the unquoted <c>title:</c> bytes (lower- or capitalised key) when present.</summary>
    /// <param name="absolutePath">Absolute path to a markdown page.</param>
    /// <returns>UTF-8 title bytes (already stripped of surrounding YAML quotes), or <see langword="null"/> when absent or unreadable.</returns>
    private static byte[]? ReadTitleBytes(string absolutePath)
    {
        try
        {
            using var handle = File.OpenHandle(absolutePath);
            var size = (int)Math.Min(FrontmatterPeekBytes, RandomAccess.GetLength(handle));
            if (size <= 0)
            {
                return null;
            }

            Span<byte> buffer = stackalloc byte[FrontmatterPeekBytes];
            var read = RandomAccess.Read(handle, buffer[..size], 0);
            var scalar = FrontmatterValueExtractor.GetScalar(buffer[..read], "title"u8);
            if (scalar.IsEmpty)
            {
                scalar = FrontmatterValueExtractor.GetScalar(buffer[..read], "Title"u8);
            }

            return scalar.IsEmpty ? null : StripYamlQuotes(scalar).ToArray();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Drops one matching pair of YAML string quotes from <paramref name="value"/>.</summary>
    /// <param name="value">Scalar bytes.</param>
    /// <returns>Unquoted bytes, or <paramref name="value"/> unchanged.</returns>
    private static ReadOnlySpan<byte> StripYamlQuotes(ReadOnlySpan<byte> value)
    {
        if (value.Length >= 2
            && (value[0] is (byte)'"' or (byte)'\'')
            && value[^1] == value[0])
        {
            return value[1..^1];
        }

        return value;
    }
}
