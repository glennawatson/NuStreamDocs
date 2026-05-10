// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>Renders the landing-page <c>index.md</c> bytes that the streaming generator pushes through the synthetic-page sink.</summary>
internal static class ApiIndexWriter
{
    /// <summary>Initial buffer capacity.</summary>
    private const int InitialBufferCapacity = 1024;

    /// <summary>Maximum digit count for an <see cref="int"/> rendered in base 10 (including the optional minus sign), used to size the <c>Order:</c> stack buffer.</summary>
    private const int MaxInt32DecimalDigits = 11;

    /// <summary>Subdirectory names that should not appear in the namespace index — the SourceDocParser emitter writes them as siblings of the package folders.</summary>
    private static readonly byte[][] InfraDirectoryNamesUtf8 =
    [
        "lib"u8.ToArray(),
        "refs"u8.ToArray(),
        "cache"u8.ToArray(),
        "_global"u8.ToArray(),
    ];

    /// <summary>Gets the default page title bytes used when the caller doesn't override it.</summary>
    private static ReadOnlySpan<byte> DefaultTitleBytes => "API Reference"u8;

    /// <summary>Renders the index page bytes for a caller-supplied namespace list.</summary>
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
        WriteNamespaceList(sink, namespaces);
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

    /// <summary>Writes a minimal YAML frontmatter block carrying just <c>Order: N</c>.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="order">Order integer.</param>
    private static void WriteOrderFrontmatter(IBufferWriter<byte> sink, int order)
    {
        WriteSpan(sink, "---\nOrder: "u8);
        Span<byte> digits = stackalloc byte[MaxInt32DecimalDigits];
        if (!System.Buffers.Text.Utf8Formatter.TryFormat(order, digits, out var written))
        {
            // Defensive: stack span is sized for the widest int32 representation.
            throw new InvalidOperationException("Failed to format Order frontmatter value.");
        }

        WriteSpan(sink, digits[..written]);
        WriteSpan(sink, "\n---\n\n"u8);
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
    /// <param name="namespaces">Sorted namespace directory names as UTF-8 byte arrays.</param>
    private static void WriteNamespaceList(IBufferWriter<byte> sink, byte[][] namespaces)
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
}
