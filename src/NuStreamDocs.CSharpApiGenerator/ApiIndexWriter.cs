// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Writes a single landing-page <c>index.md</c> at the root of the
/// generated API tree, listing every per-namespace directory the
/// emitter produced as a clickable link.
/// </summary>
/// <remarks>
/// Runs once after the per-namespace emit pass. Pages discovery picks up
/// the index.md alongside the rest of the docs tree; the navigation
/// plugin then renders a "Namespaces" entry-point at the configured
/// output subdirectory (typically <c>/api/</c>).
/// </remarks>
internal static class ApiIndexWriter
{
    /// <summary>Initial buffer capacity; a few hundred bytes for the title + intro plus 32 B per namespace covers the common case without a grow.</summary>
    private const int InitialBufferCapacity = 1024;

    /// <summary>Directory names the emitter writes alongside per-namespace content that should not appear as namespaces in the index.</summary>
    private static readonly HashSet<string> InfraDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "lib",
        "refs",
        "cache",
        "_global",
    };

    /// <summary>Gets the default page title bytes used when the caller doesn't override it.</summary>
    private static ReadOnlySpan<byte> DefaultTitleBytes => "API Reference"u8;

    /// <summary>Writes <c>{apiPath}/index.md</c> listing every non-infra subdirectory of <paramref name="apiPath"/> as a namespace link.</summary>
    /// <param name="apiPath">Absolute path to the API output root the emitter wrote.</param>
    /// <param name="title">UTF-8 page title bytes; falls back to <c>API Reference</c> when empty.</param>
    /// <param name="introduction">Optional UTF-8 intro paragraph rendered between the title and the namespace list. Empty for no intro.</param>
    /// <returns>Number of namespace directories written into the index, or zero when <paramref name="apiPath"/> doesn't exist or contains no candidate dirs.</returns>
    public static int Write(DirectoryPath apiPath, ReadOnlySpan<byte> title, ReadOnlySpan<byte> introduction)
    {
        if (!apiPath.Exists())
        {
            return 0;
        }

        var namespaces = CollectNamespaceNames(apiPath);
        if (namespaces.Length is 0)
        {
            return 0;
        }

        var sink = new ArrayBufferWriter<byte>(InitialBufferCapacity);
        WriteTitle(sink, title);
        WriteIntroduction(sink, introduction);
        WriteNamespaceList(sink, namespaces);

        var indexPath = apiPath.File("index.md");
        File.WriteAllBytes(indexPath.Value, sink.WrittenSpan.ToArray());
        return namespaces.Length;
    }

    /// <summary>Returns the ordinal-sorted set of subdirectory names under <paramref name="apiPath"/>, skipping the well-known infrastructure dirs.</summary>
    /// <param name="apiPath">API output root.</param>
    /// <returns>Sorted namespace directory names.</returns>
    private static string[] CollectNamespaceNames(DirectoryPath apiPath)
    {
        // Direct BCL call so we can iterate by index — DirectoryPath.EnumerateDirectories
        // would yield wrapper structs we'd unwrap one-by-one.
        var dirs = Directory.GetDirectories(apiPath.Value);
        var collected = new List<string>(dirs.Length);
        for (var i = 0; i < dirs.Length; i++)
        {
            var name = Path.GetFileName(dirs[i]);
            if (!InfraDirectoryNames.Contains(name))
            {
                collected.Add(name);
            }
        }

        collected.Sort(StringComparer.OrdinalIgnoreCase);
        return [.. collected];
    }

    /// <summary>Writes the <c># title</c> heading followed by a blank line.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="title">Caller-supplied UTF-8 title bytes; falls back to <see cref="DefaultTitleBytes"/> when empty.</param>
    private static void WriteTitle(IBufferWriter<byte> sink, ReadOnlySpan<byte> title)
    {
        WriteSpan(sink, "# "u8);
        WriteSpan(sink, title.IsEmpty ? DefaultTitleBytes : title);
        WriteSpan(sink, "\n\n"u8);
    }

    /// <summary>Writes the intro paragraph (if any) followed by a blank line.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="introduction">Caller-supplied UTF-8 intro bytes; nothing is written when empty.</param>
    private static void WriteIntroduction(IBufferWriter<byte> sink, ReadOnlySpan<byte> introduction)
    {
        if (introduction.IsEmpty)
        {
            return;
        }

        WriteSpan(sink, introduction);
        WriteSpan(sink, "\n\n"u8);
    }

    /// <summary>Writes the <c>## Namespaces</c> section followed by one bullet per namespace.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="namespaces">Sorted namespace directory names.</param>
    private static void WriteNamespaceList(IBufferWriter<byte> sink, string[] namespaces)
    {
        WriteSpan(sink, "## Namespaces\n\n"u8);
        for (var i = 0; i < namespaces.Length; i++)
        {
            WriteSpan(sink, "- [`"u8);
            WriteUtf8(sink, namespaces[i]);
            WriteSpan(sink, "`]("u8);
            WriteUtf8(sink, namespaces[i]);
            WriteSpan(sink, "/)\n"u8);
        }
    }

    /// <summary>Bulk-writes a UTF-8 byte span into <paramref name="sink"/>.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void WriteSpan(IBufferWriter<byte> sink, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dest = sink.GetSpan(bytes.Length);
        bytes.CopyTo(dest);
        sink.Advance(bytes.Length);
    }

    /// <summary>Encodes a string into UTF-8 and writes the bytes into <paramref name="sink"/>.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="value">Source string; nothing is written when null or empty.</param>
    private static void WriteUtf8(IBufferWriter<byte> sink, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(value);
        var dest = sink.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(value, dest);
        sink.Advance(byteCount);
    }
}
