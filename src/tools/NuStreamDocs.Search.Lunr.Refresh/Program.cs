// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace NuStreamDocs.Search.Lunr.Refresh;

/// <summary>
/// Downloads <c>lunr.min.js</c> from upstream (jsDelivr against the npm registry)
/// and drops it into <c>NuStreamDocs.Search.Lunr/Assets/</c> so the package can
/// embed it as a managed resource.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the Pagefind refresh tool. Run after every Lunr release bump:
/// </para>
/// <code>
/// dotnet run --project src/tools/NuStreamDocs.Search.Lunr.Refresh -- --version 2.3.9
/// </code>
/// <para>
/// Lunr.js doesn't publish stand-alone GitHub release assets — the canonical
/// distribution channel is npm. jsDelivr serves immutable, content-addressed
/// copies straight from the registry, which is plenty for our vendoring needs.
/// After running, bump <c>LunrAssets.PinnedVersion</c> in the package to match.
/// </para>
/// </remarks>
public static class Program
{
    /// <summary>Default Lunr release tag (npm version).</summary>
    private const string DefaultVersion = "2.3.9";

    /// <summary>Default HTTP user-agent.</summary>
    private const string UserAgent = "NuStreamDocs.Search.Lunr.Refresh/1.0";

    /// <summary>How many directory levels to walk up from <c>AppContext.BaseDirectory</c> looking for the <c>src/</c> root.</summary>
    private const int SrcRootProbeDepth = 8;

    /// <summary>Upstream asset URL template; <c>{0}</c> = version.</summary>
    private static readonly System.Text.CompositeFormat AssetUrlTemplate =
        System.Text.CompositeFormat.Parse("https://cdn.jsdelivr.net/npm/lunr@{0}/lunr.min.js");

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
        var outputPath = ValueOf(args, "--output") ?? DefaultOutputPath();
        var stdout = Console.Out;

        var assetUrl = string.Format(CultureInfo.InvariantCulture, AssetUrlTemplate, version);
        Uri assetUri = new(assetUrl);

        using HttpClientHandler handler = new()
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            CheckCertificateRevocationList = true,
        };
        using HttpClient http = new(handler);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        http.Timeout = TimeSpan.FromMinutes(2);

        await stdout.WriteLineAsync($"Lunr {version} → {outputPath}").ConfigureAwait(false);
        try
        {
            await stdout.WriteLineAsync($"  fetching {assetUrl}").ConfigureAwait(false);
            var bytes = await http.GetByteArrayAsync(assetUri).ConfigureAwait(false);
            await stdout.WriteLineAsync($"    {bytes.Length:N0} bytes").ConfigureAwait(false);

            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            await stdout.WriteLineAsync($"    SHA-256: {sha}").ConfigureAwait(false);

            var dir = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(outputPath, bytes).ConfigureAwait(false);
            await stdout.WriteLineAsync($"    → {outputPath}").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            await Console.Error.WriteLineAsync($"  error: {ex.Message}").ConfigureAwait(false);
            return 1;
        }

        await stdout.WriteLineAsync("\nDone. Remember to bump LunrAssets.PinnedVersion if the version changed.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>Resolves the default output path — the package's <c>Assets/lunr.min.js</c> file relative to this tool.</summary>
    /// <returns>Absolute path.</returns>
    private static string DefaultOutputPath()
    {
        // Tool lives at src/tools/NuStreamDocs.Search.Lunr.Refresh/; package's Assets/ folder
        // lives at src/NuStreamDocs.Search.Lunr/. Walk up from the tool's bin/ until we hit src/.
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

        return Path.Combine(srcRoot, "NuStreamDocs.Search.Lunr", "Assets", "lunr.min.js");
    }

    /// <summary>Returns the value following <paramref name="flag"/> in <paramref name="args"/>, or null.</summary>
    /// <param name="args">Argument list.</param>
    /// <param name="flag">Flag name.</param>
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
            .AppendLine("NuStreamDocs.Search.Lunr.Refresh")
            .AppendLine()
            .AppendLine("Downloads lunr.min.js from jsDelivr and writes it to")
            .AppendLine("NuStreamDocs.Search.Lunr/Assets/lunr.min.js.")
            .AppendLine()
            .AppendLine("Usage:")
            .AppendLine("  dotnet run --project src/tools/NuStreamDocs.Search.Lunr.Refresh")
            .AppendLine("    [--version <v>]                Lunr npm version (e.g. 2.3.9).")
            .AppendLine(CultureInfo.InvariantCulture, $"                                   Default: {DefaultVersion}.")
            .AppendLine("    [--output <path>]              Override the lunr.min.js destination.")
            .AppendLine("    [--help] [-h]                  Print this message.")
            .ToString();
        Console.Out.Write(help);
    }
}
