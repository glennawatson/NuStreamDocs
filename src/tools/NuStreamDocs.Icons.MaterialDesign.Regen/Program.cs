// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace NuStreamDocs.Icons.MaterialDesign.Regen;

/// <summary>
/// Regenerates the embedded MDI icon bundle from
/// <c>Templarian/MaterialDesign-SVG</c>. Run this when MDI publishes
/// a new release; the generated <c>mdi-icons.bin</c> drops into
/// <c>src/NuStreamDocs.Icons.MaterialDesign/</c> as an embedded
/// resource.
/// </summary>
/// <remarks>
/// Uses a shallow <c>git clone</c> into <see cref="Directory.CreateTempSubdirectory"/>
/// rather than ~7000 individual HTTP fetches — single git transaction,
/// no per-icon round-trip latency, no GitHub raw rate limits. Assumes
/// <c>git</c> is on the <c>PATH</c>.
/// <para>
/// Bundle byte layout (post-deflate):
/// <list type="number">
/// <item><c>uint32</c> entry count (LE).</item>
/// <item>Per entry: <c>uint16</c> name length, UTF-8 name bytes, <c>uint32</c> SVG length, UTF-8 SVG bytes.</item>
/// </list>
/// </para>
/// </remarks>
public static class Program
{
    /// <summary>Upstream MDI SVG repository.</summary>
    private const string MdiRepoUrl = "https://github.com/Templarian/MaterialDesign-SVG.git";

    /// <summary>Subdirectory in the cloned repo that holds one SVG per icon.</summary>
    private const string SvgSubdirectory = "svg";

    /// <summary>Entry point.</summary>
    /// <param name="args">CLI arguments — optional <c>--output &lt;path&gt;</c> to override the bundle location.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var outputPath = ResolveOutputPath(args);
        var stdout = Console.Out;
        await stdout.WriteLineAsync($"Writing MDI bundle to: {outputPath}").ConfigureAwait(false);

        var workdir = Directory.CreateTempSubdirectory("smkd-mdi-regen-");
        try
        {
            await stdout.WriteLineAsync($"Cloning {MdiRepoUrl} → {workdir.FullName}").ConfigureAwait(false);
            await CloneShallowAsync(MdiRepoUrl, workdir.FullName).ConfigureAwait(false);

            var svgRoot = Path.Combine(workdir.FullName, SvgSubdirectory);
            if (!Directory.Exists(svgRoot))
            {
                await Console.Error.WriteLineAsync($"  error: clone did not produce '{SvgSubdirectory}/' under {workdir.FullName}").ConfigureAwait(false);
                return 1;
            }

            await stdout.WriteLineAsync($"Reading SVGs from {svgRoot}").ConfigureAwait(false);
            var entries = await CollectEntriesAsync(svgRoot).ConfigureAwait(false);
            await stdout.WriteLineAsync($"Writing bundle with {entries.Count} entries").ConfigureAwait(false);
            await WriteBundleAsync(outputPath, entries).ConfigureAwait(false);
            await stdout.WriteLineAsync("Done.").ConfigureAwait(false);
            return 0;
        }
        finally
        {
            try
            {
                workdir.Delete(recursive: true);
            }
            catch (IOException ex)
            {
                await Console.Error.WriteLineAsync($"  warn: temp dir not fully deleted: {ex.Message}").ConfigureAwait(false);
            }
        }
    }

    /// <summary>Resolves the output path from CLI args, defaulting to the embedded-resource location under the repo.</summary>
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

    /// <summary>Runs <c>git clone --depth 1 --filter=blob:none -- &lt;repo&gt; &lt;dir&gt;</c> against the system <c>git</c>.</summary>
    /// <param name="repoUrl">Upstream URL.</param>
    /// <param name="targetDirectory">Empty directory to clone into.</param>
    /// <returns>Task tracking the clone; throws when <c>git</c> exits non-zero.</returns>
    private static async Task CloneShallowAsync(string repoUrl, string targetDirectory)
    {
        var info = new ProcessStartInfo("git")
        {
            ArgumentList = { "clone", "--depth", "1", "--single-branch", "--", repoUrl, targetDirectory },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Failed to spawn git.");
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode is 0)
        {
            return;
        }

        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        throw new InvalidOperationException($"git clone exited {process.ExitCode}: {stderr}");
    }

    /// <summary>Reads every <c>.svg</c> under <paramref name="svgRoot"/> into <c>(name, bytes)</c> pairs sorted by name.</summary>
    /// <param name="svgRoot">Path to the cloned <c>svg/</c> directory.</param>
    /// <returns>Per-icon entries in <see cref="StringComparer.Ordinal"/> order so bundle output is byte-stable.</returns>
    private static async Task<List<(string Name, byte[] Svg)>> CollectEntriesAsync(string svgRoot)
    {
        var paths = Directory.GetFiles(svgRoot, "*.svg", SearchOption.TopDirectoryOnly);
        Array.Sort(paths, StringComparer.Ordinal);
        var entries = new List<(string Name, byte[] Svg)>(paths.Length);
        for (var i = 0; i < paths.Length; i++)
        {
            var path = paths[i];
            var name = Path.GetFileNameWithoutExtension(path);
            var svg = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            entries.Add((name, svg));
        }

        return entries;
    }

    /// <summary>Encodes <paramref name="entries"/> in the bundle format and deflate-compresses to <paramref name="outputPath"/>.</summary>
    /// <param name="outputPath">Absolute output path.</param>
    /// <param name="entries">Per-icon entries.</param>
    /// <returns>Task tracking the write.</returns>
    private static async Task WriteBundleAsync(string outputPath, List<(string Name, byte[] Svg)> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var fileStream = File.Create(outputPath);
        await using var deflate = new DeflateStream(fileStream, CompressionLevel.Optimal, leaveOpen: false);

        var intBuffer = new byte[sizeof(uint)];
        var shortBuffer = new byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt32LittleEndian(intBuffer, (uint)entries.Count);
        await deflate.WriteAsync(intBuffer).ConfigureAwait(false);

        for (var i = 0; i < entries.Count; i++)
        {
            var (name, svg) = entries[i];
            var nameBytes = Encoding.UTF8.GetBytes(name);
            BinaryPrimitives.WriteUInt16LittleEndian(shortBuffer, (ushort)nameBytes.Length);
            await deflate.WriteAsync(shortBuffer).ConfigureAwait(false);
            await deflate.WriteAsync(nameBytes).ConfigureAwait(false);

            BinaryPrimitives.WriteUInt32LittleEndian(intBuffer, (uint)svg.Length);
            await deflate.WriteAsync(intBuffer).ConfigureAwait(false);
            await deflate.WriteAsync(svg).ConfigureAwait(false);
        }
    }
}
