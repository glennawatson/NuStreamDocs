// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace NuStreamDocs.Icons.MaterialDesign.Regen;

/// <summary>
/// Regenerates the embedded MDI icon bundle from
/// <c>@mdi/svg</c>'s GitHub repository. Run this when MDI publishes a
/// new release; the generated <c>mdi-icons.bin</c> drops into
/// <c>src/NuStreamDocs.Icons.MaterialDesign/</c> as an embedded
/// resource.
/// </summary>
/// <remarks>
/// The tool is intentionally allocation-naive — it runs once per MDI
/// release and isn't on any hot path. The byte layout it emits is the
/// one <c>MdiIconBundle</c> decodes:
/// <list type="number">
/// <item><c>uint32</c> entry count (LE).</item>
/// <item>Per entry: <c>uint16</c> name length, UTF-8 name bytes, <c>uint32</c> SVG length, UTF-8 SVG bytes.</item>
/// </list>
/// File is then deflate-compressed.
/// </remarks>
public static class Program
{
    /// <summary>Default MDI source — the upstream <c>@mdi/svg</c> meta + svg trees on GitHub raw.</summary>
    private const string MdiMetaUrl = "https://raw.githubusercontent.com/Templarian/MaterialDesign/master/meta.json";

    /// <summary>Per-icon SVG path — the <c>{name}</c> placeholder is filled at fetch time.</summary>
    private const string MdiSvgUrlTemplate = "https://raw.githubusercontent.com/Templarian/MaterialDesign-SVG/master/svg/{0}.svg";

    /// <summary>Entry point.</summary>
    /// <param name="args">CLI arguments — optional <c>--output &lt;path&gt;</c> to override the default bundle location.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        var outputPath = ResolveOutputPath(args);
        Console.WriteLine($"Writing MDI bundle to: {outputPath}");

        using var http = new HttpClient();
        Console.WriteLine($"Fetching {MdiMetaUrl}");
        var metaJson = await http.GetByteArrayAsync(MdiMetaUrl).ConfigureAwait(false);
        var names = ExtractIconNames(metaJson);
        Console.WriteLine($"Found {names.Count} icon names — fetching SVGs (this can take a while)");

        var entries = new List<(string Name, byte[] Svg)>(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            var url = string.Format(System.Globalization.CultureInfo.InvariantCulture, MdiSvgUrlTemplate, name);
            try
            {
                var svg = await http.GetByteArrayAsync(url).ConfigureAwait(false);
                entries.Add((name, svg));
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"  warn: skipped {name} ({ex.Message})");
            }

            if ((i % 250) is 0)
            {
                Console.WriteLine($"  {i + 1}/{names.Count}");
            }
        }

        Console.WriteLine($"Writing bundle with {entries.Count} entries");
        WriteBundle(outputPath, entries);
        Console.WriteLine("Done.");
        return 0;
    }

    /// <summary>Resolves the output path from CLI args.</summary>
    /// <param name="args">CLI args.</param>
    /// <returns>Absolute path.</returns>
    private static string ResolveOutputPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--output" or "-o")
            {
                return Path.GetFullPath(args[i + 1]);
            }
        }

        var here = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(here, "..", "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "NuStreamDocs.Icons.MaterialDesign", "mdi-icons.bin");
    }

    /// <summary>Pulls the icon-name list out of MDI's <c>meta.json</c>.</summary>
    /// <param name="metaJson">UTF-8 meta JSON bytes.</param>
    /// <returns>Icon names, alphabetised for stable bundle output.</returns>
    private static List<string> ExtractIconNames(byte[] metaJson)
    {
        using var doc = JsonDocument.Parse(metaJson);
        var names = new List<string>();
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (entry.TryGetProperty("name"u8, out var nameElement) &&
                nameElement.GetString() is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>Encodes <paramref name="entries"/> in the bundle format and deflate-compresses to <paramref name="outputPath"/>.</summary>
    /// <param name="outputPath">Absolute output path.</param>
    /// <param name="entries">Per-icon entries.</param>
    private static void WriteBundle(string outputPath, List<(string Name, byte[] Svg)> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var fileStream = File.Create(outputPath);
        using var deflate = new DeflateStream(fileStream, CompressionLevel.Optimal, leaveOpen: false);

        Span<byte> intBuffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(intBuffer, (uint)entries.Count);
        deflate.Write(intBuffer);

        foreach (var (name, svg) in entries)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            Span<byte> shortBuffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(shortBuffer, (ushort)nameBytes.Length);
            deflate.Write(shortBuffer);
            deflate.Write(nameBytes);

            BinaryPrimitives.WriteUInt32LittleEndian(intBuffer, (uint)svg.Length);
            deflate.Write(intBuffer);
            deflate.Write(svg);
        }
    }
}
