// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace NuStreamDocs.Search.Pagefind.Refresh;

/// <summary>
/// Downloads the per-RID Pagefind release binaries from upstream GitHub releases
/// and lays them out under
/// <c>NuStreamDocs.Search.Pagefind/runtimes/&lt;rid&gt;/native/</c> so the NuGet
/// package can ship them as platform-specific content.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the icon-refresh tool pattern (see
/// <c>NuStreamDocs.Icons.MaterialDesign.Regen</c>): one self-contained
/// console app under <c>src/tools/</c>, no project references, downloads
/// upstream assets, drops them into the consuming package's content tree,
/// done. Run after every Pagefind release bump:
/// </para>
/// <code>
/// dotnet run --project src/tools/NuStreamDocs.Search.Pagefind.Refresh -- --version 1.5.2
/// </code>
/// <para>
/// SHA-256 verification is enabled by default — every binary is checked
/// against the <c>.sha256</c> sidecar published alongside the tarball. Use
/// <c>--no-verify</c> only when GitHub is rate-limiting the sidecar fetches.
/// After running, bump <c>PagefindCli.PinnedVersion</c> in
/// <c>NuStreamDocs.Search.Pagefind</c> to match.
/// </para>
/// </remarks>
public static class Program
{
    /// <summary>Default Pagefind release tag — kept in sync with <c>PagefindCli.PinnedVersion</c> in the package.</summary>
    private const string DefaultVersion = "1.5.2";

    /// <summary>Filename component identifying the binary inside an extracted tarball.</summary>
    private const string BinaryStem = "pagefind";

    /// <summary>Default HTTP user-agent.</summary>
    private const string UserAgent = "NuStreamDocs.Search.Pagefind.Refresh/1.0";

    /// <summary>How many directory levels to walk up from <c>AppContext.BaseDirectory</c> looking for the <c>src/</c> root.</summary>
    private const int SrcRootProbeDepth = 8;

    /// <summary>Upstream release-asset URL template; <c>{0}</c> = version, <c>{1}</c> = filename.</summary>
    private static readonly CompositeFormat ReleaseAssetTemplate =
        CompositeFormat.Parse("https://github.com/CloudCannon/pagefind/releases/download/v{0}/{1}");

    /// <summary>The five RIDs we ship — broad .NET RIDs paired with the matching Rust target triple.</summary>
    private static readonly RidMapping[] Rids =
    [
        new("linux-x64", "x86_64-unknown-linux-musl", IsWindows: false),
        new("linux-arm64", "aarch64-unknown-linux-musl", IsWindows: false),
        new("win-x64", "x86_64-pc-windows-msvc", IsWindows: true),
        new("osx-x64", "x86_64-apple-darwin", IsWindows: false),
        new("osx-arm64", "aarch64-apple-darwin", IsWindows: false),
    ];

    /// <summary>Entry point.</summary>
    /// <param name="args">CLI args.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintUsage();
            return 0;
        }

        var version = ValueOf(args, "--version") ?? DefaultVersion;
        var outputBase = ValueOf(args, "--output") ?? DefaultOutputBase();
        var ridFilter = ValueOf(args, "--rids");
        var verify = !HasFlag(args, "--no-verify");

        var stdout = Console.Out;
        await stdout.WriteLineAsync($"Pagefind {version} → {outputBase}").ConfigureAwait(false);

        var selected = SelectRids(ridFilter);
        if (selected.Count == 0)
        {
            await Console.Error.WriteLineAsync($"  error: no RIDs matched filter '{ridFilter}'.").ConfigureAwait(false);
            return 1;
        }

        using HttpClientHandler handler = new();
        handler.AutomaticDecompression = System.Net.DecompressionMethods.All;
        handler.CheckCertificateRevocationList = true;
        using HttpClient http = new(handler);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        http.Timeout = TimeSpan.FromMinutes(5);

        var failures = 0;
        for (var i = 0; i < selected.Count; i++)
        {
            var rid = selected[i];
            try
            {
                await stdout.WriteLineAsync($"\n[{rid.Net}] {rid.Rust}").ConfigureAwait(false);
                await RefreshOneAsync(http, version, rid, outputBase, verify, stdout).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException)
            {
                await Console.Error.WriteLineAsync($"  error: {ex.Message}").ConfigureAwait(false);
                failures++;
            }
        }

        if (failures > 0)
        {
            await Console.Error.WriteLineAsync($"\n{failures} of {selected.Count} RID(s) failed.").ConfigureAwait(false);
            return 1;
        }

        await stdout.WriteLineAsync("\nDone. Remember to bump PagefindCli.PinnedVersion if the version changed.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>Refreshes the binary for one RID — download, verify, extract, write.</summary>
    /// <param name="http">HTTP client.</param>
    /// <param name="version">Pagefind release tag without the leading <c>v</c>.</param>
    /// <param name="rid">RID mapping.</param>
    /// <param name="outputBase">Absolute path to the package's <c>runtimes/</c> folder.</param>
    /// <param name="verify">When true, fetch the upstream <c>.sha256</c> sidecar and verify the tarball.</param>
    /// <param name="stdout">Standard-output writer for progress lines.</param>
    /// <returns>A task that completes when the binary lands on disk.</returns>
    private static async Task RefreshOneAsync(HttpClient http, string version, RidMapping rid, string outputBase, bool verify, TextWriter stdout)
    {
        var assetName = $"pagefind-v{version}-{rid.Rust}.tar.gz";
        var assetUrl = string.Format(CultureInfo.InvariantCulture, ReleaseAssetTemplate, version, assetName);
        Uri assetUri = new(assetUrl);

        await stdout.WriteLineAsync($"  fetching {assetUrl}").ConfigureAwait(false);
        var tarball = await http.GetByteArrayAsync(assetUri).ConfigureAwait(false);
        await stdout.WriteLineAsync($"    {tarball.Length:N0} bytes").ConfigureAwait(false);

        if (verify)
        {
            await stdout.WriteAsync("    verifying SHA-256… ").ConfigureAwait(false);
            Uri sigUri = new(assetUrl + ".sha256");
            var sigText = await http.GetStringAsync(sigUri).ConfigureAwait(false);
            var expected = ParseShaSidecar(sigText);
            var actual = Convert.ToHexStringLower(SHA256.HashData(tarball));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SHA-256 mismatch — expected {expected}, got {actual}.");
            }

            await stdout.WriteLineAsync("ok").ConfigureAwait(false);
        }

        var binaryFileName = rid.IsWindows ? BinaryStem + ".exe" : BinaryStem;
        var extractedBytes = ExtractBinaryBytes(tarball, binaryFileName);
        var targetDir = Path.Combine(outputBase, rid.Net, "native");
        Directory.CreateDirectory(targetDir);
        var targetPath = Path.Combine(targetDir, binaryFileName);
        await File.WriteAllBytesAsync(targetPath, extractedBytes).ConfigureAwait(false);

        if (!rid.IsWindows && !OperatingSystem.IsWindows())
        {
            // Make the binary executable for the matching host platform; for cross-RID writes
            // (e.g. emitting linux-x64 from a macOS host) the consumer's NuGet restore re-applies
            // the +x bit anyway, so this is best-effort and silent on failure.
            try
            {
                const UnixFileMode ExecutableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                File.SetUnixFileMode(targetPath, ExecutableMode);
            }
            catch (PlatformNotSupportedException)
            {
                // Tolerate: caller will set the bit on the consumer side.
            }
        }

        await stdout.WriteLineAsync($"    → {targetPath} ({extractedBytes.Length:N0} bytes)").ConfigureAwait(false);
    }

    /// <summary>Extracts the named binary from a gzipped tarball blob.</summary>
    /// <param name="tarballBytes">Bytes of the <c>.tar.gz</c> download.</param>
    /// <param name="binaryFileName">Filename of the binary to extract (e.g. <c>pagefind</c> or <c>pagefind.exe</c>).</param>
    /// <returns>The binary's bytes.</returns>
    /// <exception cref="InvalidOperationException">If no entry with the requested name exists in the tarball.</exception>
    private static byte[] ExtractBinaryBytes(byte[] tarballBytes, string binaryFileName)
    {
        using MemoryStream gz = new(tarballBytes, writable: false);
        using GZipStream raw = new(gz, CompressionMode.Decompress);
        using TarReader reader = new(raw);
        while (reader.GetNextEntry() is { } entry)
        {
            var leaf = Path.GetFileName(entry.Name);
            if (entry.EntryType != TarEntryType.RegularFile ||
                !string.Equals(leaf, binaryFileName, StringComparison.Ordinal) ||
                entry.DataStream is null)
            {
                continue;
            }

            using MemoryStream sink = new();
            entry.DataStream.CopyTo(sink);
            return sink.ToArray();
        }

        throw new InvalidOperationException($"Tarball contained no entry named '{binaryFileName}'.");
    }

    /// <summary>Parses a <c>sha256sum</c> sidecar (<c>"&lt;hex&gt;  &lt;filename&gt;"</c>).</summary>
    /// <param name="sidecarText">Raw sidecar contents.</param>
    /// <returns>Lowercase hex SHA-256.</returns>
    private static string ParseShaSidecar(string sidecarText)
    {
        var firstWord = sidecarText.AsSpan().Trim();
        var spaceIndex = firstWord.IndexOf(' ');
        if (spaceIndex > 0)
        {
            firstWord = firstWord[..spaceIndex];
        }

        return firstWord.ToString().ToLowerInvariant();
    }

    /// <summary>Filters the <see cref="Rids"/> table by a comma-separated RID filter (or returns all when null).</summary>
    /// <param name="filter">Optional comma-separated list (e.g. <c>"linux-x64,osx-arm64"</c>).</param>
    /// <returns>The matching RIDs in declaration order.</returns>
    private static List<RidMapping> SelectRids(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            List<RidMapping> all = new(Rids.Length);
            for (var i = 0; i < Rids.Length; i++)
            {
                all.Add(Rids[i]);
            }

            return all;
        }

        var wanted = new HashSet<string>(filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
        List<RidMapping> selected = new(wanted.Count);
        for (var i = 0; i < Rids.Length; i++)
        {
            if (wanted.Contains(Rids[i].Net))
            {
                selected.Add(Rids[i]);
            }
        }

        return selected;
    }

    /// <summary>Resolves the default output base — the package's <c>runtimes/</c> folder relative to this tool.</summary>
    /// <returns>Absolute path.</returns>
    private static string DefaultOutputBase()
    {
        // Tool lives at src/tools/NuStreamDocs.Search.Pagefind.Refresh/; package's runtimes/ folder
        // lives at src/NuStreamDocs.Search.Pagefind/. Walk up from the tool's bin/ until we hit src/.
        var srcRoot = AppContext.BaseDirectory;
        for (var hops = 0; hops < SrcRootProbeDepth && Path.GetFileName(srcRoot) != "src"; hops++)
        {
            var parent = Path.GetDirectoryName(srcRoot);
            if (parent is null)
            {
                break;
            }

            srcRoot = parent;
        }

        return Path.Combine(srcRoot, "NuStreamDocs.Search.Pagefind", "runtimes");
    }

    /// <summary>Returns the value following <paramref name="flag"/> in <paramref name="args"/>, or null.</summary>
    /// <param name="args">Argument list.</param>
    /// <param name="flag">Flag name (e.g. <c>--version</c>).</param>
    /// <returns>Following value, or null when missing or last.</returns>
    private static string? ValueOf(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="flag"/> appears as a standalone arg.</summary>
    /// <param name="args">Argument list.</param>
    /// <param name="flag">Flag name.</param>
    /// <returns>Presence indicator.</returns>
    private static bool HasFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Prints CLI usage to stdout.</summary>
    private static void PrintUsage()
    {
        var help = new StringBuilder()
            .AppendLine("NuStreamDocs.Search.Pagefind.Refresh")
            .AppendLine()
            .AppendLine("Downloads per-RID Pagefind binaries from GitHub releases and lays")
            .AppendLine("them out under NuStreamDocs.Search.Pagefind/runtimes/<rid>/native/.")
            .AppendLine()
            .AppendLine("Usage:")
            .AppendLine("  dotnet run --project src/tools/NuStreamDocs.Search.Pagefind.Refresh")
            .AppendLine("    [--version <v>]                Pagefind release tag without leading 'v'.")
            .AppendLine(CultureInfo.InvariantCulture, $"                                   Default: {DefaultVersion}.")
            .AppendLine("    [--output <dir>]               Override the runtimes/ output base.")
            .AppendLine("    [--rids <list>]                Comma-separated RID filter")
            .AppendLine("                                   (linux-x64,linux-arm64,win-x64,osx-x64,osx-arm64).")
            .AppendLine("                                   Default: all.")
            .AppendLine("    [--no-verify]                  Skip SHA-256 verification.")
            .AppendLine("    [--help] [-h]                  Print this message.")
            .ToString();
        Console.Out.Write(help);
    }

    /// <summary>One row of the RID translation table.</summary>
    /// <param name="Net">.NET runtime identifier (e.g. <c>linux-x64</c>) — the folder name we ship under <c>runtimes/</c>.</param>
    /// <param name="Rust">Rust target triple (e.g. <c>x86_64-unknown-linux-musl</c>) — used to build the upstream tarball name.</param>
    /// <param name="IsWindows">Whether the binary inside the tarball is named <c>pagefind.exe</c>.</param>
    private readonly record struct RidMapping(string Net, string Rust, bool IsWindows);
}
