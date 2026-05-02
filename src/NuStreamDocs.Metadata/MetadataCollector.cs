// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Metadata;

/// <summary>
/// Walks the input root, reads directory-level (<c>_meta.yml</c>) and
/// per-page sidecar (<c>page.md.meta.yml</c>) metadata files, and
/// builds a <see cref="MetadataRegistry"/> mapping each Markdown page
/// to its merged-frontmatter byte payload.
/// </summary>
internal static class MetadataCollector
{
    /// <summary>Markdown extension recognized by the walk.</summary>
    private const string MarkdownExtension = ".md";

    /// <summary>Builds a registry for <paramref name="inputRoot"/> using <paramref name="options"/>.</summary>
    /// <param name="inputRoot">Absolute docs root.</param>
    /// <param name="options">Metadata options.</param>
    /// <returns>A populated registry; <see cref="MetadataRegistry.Empty"/> when no metadata files were found.</returns>
    public static MetadataRegistry Build(string inputRoot, in MetadataOptions options)
    {
        if (!Directory.Exists(inputRoot))
        {
            return MetadataRegistry.Empty;
        }

        var directoryFile = options.DirectoryFileName;
        var sidecarSuffix = options.SidecarSuffix;
        var directoryStack = ReadDirectoryFiles(inputRoot, directoryFile);

        var byPath = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        Walk(inputRoot, inputRoot, directoryStack, sidecarSuffix, byPath);
        if (byPath.Count is 0)
        {
            return MetadataRegistry.Empty;
        }

        return new(byPath.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Reads every <paramref name="directoryFile"/> rooted at <paramref name="inputRoot"/> into a path → bytes dictionary, used as a fast lookup during the walk.</summary>
    /// <param name="inputRoot">Absolute docs root.</param>
    /// <param name="directoryFile">Directory-metadata filename.</param>
    /// <returns>Absolute directory path → file bytes.</returns>
    private static Dictionary<string, byte[]> ReadDirectoryFiles(string inputRoot, string directoryFile)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(inputRoot, directoryFile, SearchOption.AllDirectories))
        {
            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }

            result[dir] = File.ReadAllBytes(path);
        }

        return result;
    }

    /// <summary>Recursively walks <paramref name="directory"/>, accumulating directory-level metadata into <paramref name="byPath"/> for every <c>.md</c> page found.</summary>
    /// <param name="root">Absolute docs root (constant across recursion).</param>
    /// <param name="directory">Directory currently being walked.</param>
    /// <param name="directoryStack">All directory-metadata files keyed by absolute directory.</param>
    /// <param name="sidecarSuffix">Per-page sidecar suffix.</param>
    /// <param name="byPath">Accumulator keyed by forward-slash-normalized relative path.</param>
    private static void Walk(string root, string directory, Dictionary<string, byte[]> directoryStack, string sidecarSuffix, Dictionary<string, byte[]> byPath)
    {
        var inheritedChain = CollectInheritedChain(root, directory, directoryStack);
        var files = Directory.GetFiles(directory, "*" + MarkdownExtension, SearchOption.TopDirectoryOnly);
        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var sidecarPath = file + sidecarSuffix;
            var sidecarBytes = File.Exists(sidecarPath) ? File.ReadAllBytes(sidecarPath) : [];

            var merged = MergeChain(inheritedChain, sidecarBytes);
            if (merged.Length is 0)
            {
                continue;
            }

            byPath[relative] = merged;
        }

        var subdirectories = Directory.GetDirectories(directory);
        for (var i = 0; i < subdirectories.Length; i++)
        {
            Walk(root, subdirectories[i], directoryStack, sidecarSuffix, byPath);
        }
    }

    /// <summary>Builds the inheritance chain for <paramref name="directory"/> — root-most first, current last.</summary>
    /// <param name="root">Absolute docs root.</param>
    /// <param name="directory">Directory whose chain to assemble.</param>
    /// <param name="directoryStack">All directory-metadata files.</param>
    /// <returns>Bytes per ancestor in inheritance order; closer ancestors win.</returns>
    private static byte[][] CollectInheritedChain(string root, string directory, Dictionary<string, byte[]> directoryStack)
    {
        var chain = new List<byte[]>(8);
        var cursor = directory;
        while (true)
        {
            if (directoryStack.TryGetValue(cursor, out var bytes))
            {
                chain.Add(bytes);
            }

            if (string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parent = Path.GetDirectoryName(cursor);
            if (string.IsNullOrEmpty(parent))
            {
                break;
            }

            cursor = parent;
        }

        chain.Reverse();
        return [.. chain];
    }

    /// <summary>Merges <paramref name="chain"/> + <paramref name="sidecar"/> into a single deduplicated YAML body. Closer-to-page sources override; later-defined keys win.</summary>
    /// <param name="chain">Ancestor-first chain of directory-metadata bytes.</param>
    /// <param name="sidecar">Sidecar bytes (may be empty).</param>
    /// <returns>Merged YAML body bytes (no surrounding <c>---</c>); empty when nothing to inject.</returns>
    private static byte[] MergeChain(byte[][] chain, byte[] sidecar)
    {
        if (chain.Length is 0 && sidecar.Length is 0)
        {
            return [];
        }

        var sink = new ArrayBufferWriter<byte>(initialCapacity: 256);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Iterate from highest-priority (sidecar, then closest ancestor) to
        // lowest. First-write-wins keeps the highest-priority value.
        if (sidecar.Length > 0)
        {
            AppendFreshKeys(sidecar, seen, sink);
        }

        for (var i = chain.Length - 1; i >= 0; i--)
        {
            AppendFreshKeys(chain[i], seen, sink);
        }

        return sink.WrittenCount is 0 ? [] : [.. sink.WrittenSpan];
    }

    /// <summary>Appends every top-level key from <paramref name="source"/> to <paramref name="sink"/> that hasn't already been seen.</summary>
    /// <param name="source">Source YAML bytes.</param>
    /// <param name="seen">Keys already written; updated in place.</param>
    /// <param name="sink">Output sink.</param>
    private static void AppendFreshKeys(ReadOnlySpan<byte> source, HashSet<string> seen, ArrayBufferWriter<byte> sink)
    {
        var cursor = 0;
        while (cursor < source.Length)
        {
            var lineStart = cursor;
            var lineEnd = YamlByteScanner.LineEnd(source, cursor);
            var line = source[lineStart..lineEnd];

            if (!YamlByteScanner.IsTopLevelKey(line))
            {
                cursor = lineEnd;
                continue;
            }

            var key = YamlByteScanner.KeyOf(line);
            var keyString = System.Text.Encoding.UTF8.GetString(key);
            if (!seen.Add(keyString))
            {
                cursor = YamlByteScanner.AdvancePastValue(source, lineEnd);
                continue;
            }

            var valueEnd = YamlByteScanner.AdvancePastValue(source, lineEnd);
            var block = source[lineStart..valueEnd];
            sink.Write(block);
            cursor = valueEnd;
        }
    }
}
