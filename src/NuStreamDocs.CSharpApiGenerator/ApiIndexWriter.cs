// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>Writes the landing-page <c>index.md</c> at the API output root.</summary>
internal static class ApiIndexWriter
{
    /// <summary>Initial buffer capacity.</summary>
    private const int InitialBufferCapacity = 1024;

    /// <summary>Subdirectory names that should not appear in the namespace index (case-sensitive on disk; matches the emitter's lower-cased / underscored naming).</summary>
    private static readonly HashSet<string> InfraDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "lib",
        "refs",
        "cache",
        "_global"
    };

    /// <summary>
    /// UTF-8 byte form of <see cref="InfraDirectoryNames"/> used by the byte-based
    /// <see cref="IsInfraDirectory"/> predicate so the streaming sink never has to
    /// round-trip through <see cref="string"/>.
    /// </summary>
    private static readonly byte[][] InfraDirectoryNamesUtf8 =
    [
        "lib"u8.ToArray(),
        "refs"u8.ToArray(),
        "cache"u8.ToArray(),
        "_global"u8.ToArray(),
    ];

    /// <summary>Gets the default page title bytes used when the caller doesn't override it.</summary>
    private static ReadOnlySpan<byte> DefaultTitleBytes => "API Reference"u8;

    /// <summary>Renders the index page bytes for a caller-supplied namespace list, without touching disk.</summary>
    /// <param name="namespaces">Ordered namespace folder names as UTF-8 byte arrays.</param>
    /// <param name="title">UTF-8 page title bytes; falls back to <c>API Reference</c> when empty.</param>
    /// <param name="introduction">Optional UTF-8 intro paragraph rendered between the title and the namespace list.</param>
    /// <param name="order">Optional <c>Order:</c> integer; emitted as a YAML frontmatter block at the top of the page when set.</param>
    /// <returns>The rendered UTF-8 page bytes, or an empty array when <paramref name="namespaces"/> is empty.</returns>
    public static byte[] BuildBytes(byte[][] namespaces, ReadOnlySpan<byte> title, ReadOnlySpan<byte> introduction, int? order)
    {
        ArgumentNullException.ThrowIfNull(namespaces);
        if (namespaces.Length is 0)
        {
            return [];
        }

        ArrayBufferWriter<byte> sink = new(InitialBufferCapacity);
        if (order is { } o)
        {
            WriteOrderFrontmatter(sink, o);
        }

        WriteTitle(sink, title);
        WriteIntroduction(sink, introduction);
        WriteNamespaceListBytes(sink, namespaces);
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Returns true when <paramref name="name"/> is a known infrastructure folder that should not appear in the namespace index.</summary>
    /// <param name="name">UTF-8 folder-name bytes (top-level segment of an emitted page path).</param>
    /// <returns>True when the folder should be filtered out.</returns>
    public static bool IsInfraDirectory(ReadOnlySpan<byte> name)
    {
        for (var i = 0; i < InfraDirectoryNamesUtf8.Length; i++)
        {
            if (name.SequenceEqual(InfraDirectoryNamesUtf8[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes <c>{apiPath}/index.md</c> listing every non-infra subdirectory of <paramref name="apiPath"/> as a namespace link.</summary>
    /// <param name="apiPath">Absolute path to the API output root the emitter wrote.</param>
    /// <param name="title">UTF-8 page title bytes; falls back to <c>API Reference</c> when empty.</param>
    /// <param name="introduction">Optional UTF-8 intro paragraph rendered between the title and the namespace list. Empty for no intro.</param>
    /// <returns>Number of namespace directories written into the index, or zero when <paramref name="apiPath"/> doesn't exist or contains no candidate dirs.</returns>
    public static int Write(DirectoryPath apiPath, ReadOnlySpan<byte> title, ReadOnlySpan<byte> introduction) =>
        Write(apiPath, title, introduction, order: null);

    /// <summary>Writes <c>{apiPath}/index.md</c> listing every non-infra subdirectory and (optionally) prepending an <c>Order:</c> frontmatter block.</summary>
    /// <param name="apiPath">Absolute path to the API output root the emitter wrote.</param>
    /// <param name="title">UTF-8 page title bytes; falls back to <c>API Reference</c> when empty.</param>
    /// <param name="introduction">Optional UTF-8 intro paragraph rendered between the title and the namespace list.</param>
    /// <param name="order">Optional <c>Order:</c> integer; emitted as a YAML frontmatter block at the top of the page when set.</param>
    /// <returns>Number of namespace directories written into the index, or zero when <paramref name="apiPath"/> doesn't exist or contains no candidate dirs.</returns>
    public static int Write(DirectoryPath apiPath, ReadOnlySpan<byte> title, ReadOnlySpan<byte> introduction, int? order)
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

        ArrayBufferWriter<byte> sink = new(InitialBufferCapacity);
        if (order is { } o)
        {
            WriteOrderFrontmatter(sink, o);
        }

        WriteTitle(sink, title);
        WriteIntroduction(sink, introduction);
        WriteNamespaceList(sink, namespaces);

        var indexPath = apiPath.File("index.md");
        File.WriteAllBytes(indexPath.Value, sink.WrittenSpan.ToArray());
        return namespaces.Length;
    }

    /// <summary>Writes a minimal YAML frontmatter block carrying just <c>Order: N</c>.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="order">Order integer.</param>
    private static void WriteOrderFrontmatter(IBufferWriter<byte> sink, int order)
    {
        WriteSpan(sink, "---\nOrder: "u8);
        WriteUtf8(sink, order.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteSpan(sink, "\n---\n\n"u8);
    }

    /// <summary>Returns the ordinal-sorted namespace subdirectory names under <paramref name="apiPath"/>.</summary>
    /// <param name="apiPath">API output root.</param>
    /// <returns>Sorted namespace directory names.</returns>
    private static string[] CollectNamespaceNames(DirectoryPath apiPath)
    {
        var dirs = Directory.GetDirectories(apiPath.Value);
        List<string> collected = new(dirs.Length);
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
    /// <param name="namespaces">Sorted namespace directory names (string form; from the disk-scan path).</param>
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

    /// <summary>Byte-shaped equivalent of <see cref="WriteNamespaceList"/> for the streaming sink path.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="namespaces">Sorted namespace directory names as UTF-8 byte arrays.</param>
    private static void WriteNamespaceListBytes(IBufferWriter<byte> sink, byte[][] namespaces)
    {
        WriteSpan(sink, "## Namespaces\n\n"u8);
        for (var i = 0; i < namespaces.Length; i++)
        {
            WriteSpan(sink, "- [`"u8);
            WriteSpan(sink, namespaces[i]);
            WriteSpan(sink, "`]("u8);
            WriteSpan(sink, namespaces[i]);
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
