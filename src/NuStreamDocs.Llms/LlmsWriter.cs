// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Llms;

/// <summary>Formats and writes <c>llms.txt</c>, <c>llms-full.txt</c>, and clean <c>.md</c> page siblings.</summary>
internal static class LlmsWriter
{
    /// <summary>Initial buffer size for <c>llms.txt</c>.</summary>
    private const int InitialIndexBytes = 4096;

    /// <summary>Initial buffer size for <c>llms-full.txt</c>.</summary>
    private const int InitialFullBytes = 16_384;

    /// <summary>Extra byte headroom for sibling heading and newline framing.</summary>
    private const int SiblingHeaderOverhead = 16;

    /// <summary>Writes <c>llms.txt</c>, optional <c>llms-full.txt</c>, and optional <c>.md</c> siblings under <paramref name="outputRoot"/>.</summary>
    /// <param name="outputRoot">Site output directory.</param>
    /// <param name="options">Configured LLM options.</param>
    /// <param name="siteName">Configured UTF-8 site name (may be empty).</param>
    /// <param name="baseUrl">Normalized UTF-8 base URL ending with <c>/</c>, or <c>null</c> when unconfigured.</param>
    /// <param name="entries">Sorted page entries.</param>
    internal static void WriteOutputs(
        in DirectoryPath outputRoot,
        in LlmsOptions options,
        ReadOnlySpan<byte> siteName,
        byte[]? baseUrl,
        ReadOnlySpan<LlmsPageEntry> entries)
    {
        _ = outputRoot.Create();

        ArrayBufferWriter<byte> indexSink = new(InitialIndexBytes);
        BuildIndex(options, siteName, baseUrl, entries, indexSink);
        outputRoot.File("llms.txt").WriteAllBytes(indexSink.WrittenSpan);

        if (options.EmitFullText)
        {
            ArrayBufferWriter<byte> fullSink = new(InitialFullBytes);
            BuildFullText(siteName, baseUrl, entries, fullSink);
            outputRoot.File("llms-full.txt").WriteAllBytes(fullSink.WrittenSpan);
        }

        if (!options.EmitMarkdownSiblings)
        {
            return;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            WriteSibling(outputRoot, entries[i]);
        }
    }

    /// <summary>Writes the structured <c>llms.txt</c> directory into <paramref name="sink"/>.</summary>
    /// <param name="options">Configured LLM options.</param>
    /// <param name="siteName">Configured UTF-8 site name.</param>
    /// <param name="baseUrl">Normalized UTF-8 base URL or <c>null</c>.</param>
    /// <param name="entries">Sorted page entries.</param>
    /// <param name="sink">UTF-8 destination buffer.</param>
    internal static void BuildIndex(
        in LlmsOptions options,
        ReadOnlySpan<byte> siteName,
        byte[]? baseUrl,
        ReadOnlySpan<LlmsPageEntry> entries,
        IBufferWriter<byte> sink)
    {
        sink.Write("# "u8);
        sink.Write(siteName.IsEmpty ? "Documentation"u8 : siteName);
        sink.Write("\n"u8);

        if (options.SiteSummary.Length > 0)
        {
            sink.Write("\n> "u8);
            sink.Write(options.SiteSummary);
            sink.Write("\n"u8);
        }

        ReadOnlySpan<byte> currentSection = default;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (!currentSection.SequenceEqual(entry.Section))
            {
                currentSection = entry.Section;
                sink.Write("\n## "u8);
                sink.Write(currentSection);
                sink.Write("\n\n"u8);
            }

            WriteIndexItem(sink, baseUrl, entry);
        }
    }

    /// <summary>Writes the concatenated <c>llms-full.txt</c> document into <paramref name="sink"/>.</summary>
    /// <param name="siteName">Configured UTF-8 site name.</param>
    /// <param name="baseUrl">Normalized UTF-8 base URL or <c>null</c>.</param>
    /// <param name="entries">Sorted page entries.</param>
    /// <param name="sink">UTF-8 destination buffer.</param>
    internal static void BuildFullText(
        ReadOnlySpan<byte> siteName,
        byte[]? baseUrl,
        ReadOnlySpan<LlmsPageEntry> entries,
        IBufferWriter<byte> sink)
    {
        sink.Write("# "u8);
        sink.Write(siteName.IsEmpty ? "Documentation"u8 : siteName);
        sink.Write("\n"u8);

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            sink.Write("\n---\n\n# "u8);
            sink.Write(entry.Title);
            sink.Write("\nSource: "u8);
            WriteResolvedUrl(sink, baseUrl, entry.UrlPath);
            sink.Write("\n\n"u8);
            if (entry.BodyMarkdown.Length is 0)
            {
                continue;
            }

            sink.Write(entry.BodyMarkdown);
            sink.Write("\n"u8);
        }
    }

    /// <summary>Normalizes <paramref name="raw"/> to a UTF-8 base URL ending with <c>/</c>, or <c>null</c> when blank.</summary>
    /// <param name="raw">Raw site URL bytes.</param>
    /// <returns>Normalized UTF-8 base URL or <c>null</c>.</returns>
    internal static byte[]? NormalizeBaseUrl(ReadOnlySpan<byte> raw)
    {
        var trimmed = raw.Trim((byte)' ').Trim((byte)'\t');
        if (trimmed.IsEmpty)
        {
            return null;
        }

        if (trimmed[^1] is (byte)'/')
        {
            return trimmed.ToArray();
        }

        var dst = new byte[trimmed.Length + 1];
        trimmed.CopyTo(dst);
        dst[^1] = (byte)'/';
        return dst;
    }

    /// <summary>Compares two <see cref="LlmsPageEntry"/> values by section then URL path for deterministic output.</summary>
    /// <param name="left">First entry.</param>
    /// <param name="right">Second entry.</param>
    /// <returns>Ordinal byte comparison result.</returns>
    internal static int CompareEntries(LlmsPageEntry left, LlmsPageEntry right)
    {
        var sec = left.Section.AsSpan().SequenceCompareTo(right.Section);
        return sec != 0 ? sec : left.UrlPath.AsSpan().SequenceCompareTo(right.UrlPath);
    }

    /// <summary>Writes a single bullet item in <c>llms.txt</c>.</summary>
    /// <param name="sink">UTF-8 destination buffer.</param>
    /// <param name="baseUrl">Normalized base URL or <c>null</c>.</param>
    /// <param name="entry">Page entry.</param>
    private static void WriteIndexItem(IBufferWriter<byte> sink, byte[]? baseUrl, in LlmsPageEntry entry)
    {
        sink.Write("- ["u8);
        sink.Write(entry.Title);
        sink.Write("]("u8);
        WriteResolvedUrl(sink, baseUrl, entry.UrlPath);
        sink.Write(")"u8);
        if (entry.Summary.Length > 0)
        {
            sink.Write(": "u8);
            sink.Write(entry.Summary);
        }

        sink.Write("\n"u8);
    }

    /// <summary>Writes a full or root-relative URL into <paramref name="sink"/>.</summary>
    /// <param name="sink">UTF-8 destination buffer.</param>
    /// <param name="baseUrl">Normalized base URL ending in <c>/</c>, or <c>null</c>.</param>
    /// <param name="urlPath">Page relative URL path.</param>
    private static void WriteResolvedUrl(IBufferWriter<byte> sink, byte[]? baseUrl, ReadOnlySpan<byte> urlPath)
    {
        var normalized = urlPath.SequenceEqual("./"u8) ? ReadOnlySpan<byte>.Empty : urlPath;
        sink.Write(baseUrl is not null ? baseUrl : "/"u8);
        sink.Write(normalized);
    }

    /// <summary>Writes a clean Markdown sibling file under <paramref name="outputRoot"/>.</summary>
    /// <param name="outputRoot">Site output directory.</param>
    /// <param name="entry">Page entry.</param>
    private static void WriteSibling(in DirectoryPath outputRoot, in LlmsPageEntry entry)
    {
        var target = outputRoot.File(entry.RelativePath);
        _ = target.Directory.Create();
        ArrayBufferWriter<byte> sink = new(entry.Title.Length + entry.BodyMarkdown.Length + SiblingHeaderOverhead);
        if (!entry.BodyMarkdown.AsSpan().StartsWith("# "u8))
        {
            sink.Write("# "u8);
            sink.Write(entry.Title);
            sink.Write(entry.BodyMarkdown.Length > 0 ? "\n\n"u8 : "\n"u8);
        }

        if (entry.BodyMarkdown.Length > 0)
        {
            sink.Write(entry.BodyMarkdown);
            sink.Write("\n"u8);
        }

        target.WriteAllBytes(sink.WrittenSpan);
    }
}
