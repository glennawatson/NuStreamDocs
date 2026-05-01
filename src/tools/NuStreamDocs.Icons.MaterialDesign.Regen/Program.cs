// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NuStreamDocs.Icons.MaterialDesign.Regen;

/// <summary>
/// Regenerates the embedded MDI icon catalogue from
/// <c>Templarian/MaterialDesign-SVG</c>. Emits a single
/// <c>MdiIconData.g.cs</c> file containing a bucket-by-length nested
/// switch — outer dispatch on name length picks the right per-length
/// method, inner dispatch switches on the first UTF-8 byte before
/// verbatim-matching the name and returning the SVG bytes as a
/// <c>"..."u8</c> literal.
/// </summary>
/// <remarks>
/// Generated layout — chosen for the perf characteristics laid out in
/// the assembly's README:
/// <list type="bullet">
/// <item>Zero startup cost — no resource decode, no dictionary build.</item>
/// <item>~50–100 ns per lookup — JIT lowers each length bucket to a first-byte switch plus a tight exact-match chain.</item>
/// <item>Avoids the 64 KB IL-size-per-method limit — each per-length partial stays small.</item>
/// <item>Zero managed heap retention — every SVG is a <c>"..."u8</c> blob in the assembly's <c>#Blob</c> heap.</item>
/// </list>
/// Run on every MDI release (~quarterly major bumps). Assumes <c>git</c> is on the <c>PATH</c>.
/// </remarks>
public static class Program
{
    /// <summary>Upstream MDI SVG repository.</summary>
    private const string MdiRepoUrl = "https://github.com/Templarian/MaterialDesign-SVG.git";

    /// <summary>Subdirectory in the cloned repo that holds one SVG per icon.</summary>
    private const string SvgSubdirectory = "svg";

    /// <summary>Lowest printable ASCII byte — bytes below this get hex-escaped in <see cref="Utf8Literal(System.ReadOnlySpan{byte})"/>.</summary>
    private const byte FirstPrintableAsciiByte = 0x20;

    /// <summary>Highest printable ASCII byte — bytes at or above this (i.e. DEL + non-ASCII) get hex-escaped.</summary>
    private const byte LastPrintableAsciiByte = 0x7E;

    /// <summary>Sonar analyser category — repeated across every suppression we emit.</summary>
    private const string SonarCategory = "Sonar Code Smell";

    /// <summary>StyleCop analyser category — repeated across every suppression we emit.</summary>
    private const string StyleCopCategory = "StyleCop";

    /// <summary>Entry point.</summary>
    /// <param name="args">CLI args — optional <c>--output &lt;path&gt;</c> to override the generated-file location.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var outputPath = ResolveOutputPath(args);
        var stdout = Console.Out;
        await stdout.WriteLineAsync($"Writing MDI catalogue to: {outputPath}").ConfigureAwait(false);

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
            await stdout.WriteLineAsync($"Generating catalogue with {entries.Count} entries").ConfigureAwait(false);
            await WriteGeneratedFileAsync(outputPath, entries).ConfigureAwait(false);
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

    /// <summary>Resolves the output path from CLI args, defaulting to <c>MdiIconData.g.cs</c> under the assembly.</summary>
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
        return Path.Combine(repoRoot, "src", "NuStreamDocs.Icons.MaterialDesign", "MdiIconData.g.cs");
    }

    /// <summary>Runs <c>git clone --depth 1 --single-branch -- &lt;repo&gt; &lt;dir&gt;</c> against the system <c>git</c>.</summary>
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
    /// <returns>Per-icon entries in <see cref="StringComparer.Ordinal"/> order so generated output is byte-stable.</returns>
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

    /// <summary>Writes the generated catalogue file (<c>MdiIconData.g.cs</c>) for <paramref name="entries"/>.</summary>
    /// <param name="outputPath">Absolute output path.</param>
    /// <param name="entries">Per-icon entries.</param>
    /// <returns>Task tracking the write.</returns>
    private static async Task WriteGeneratedFileAsync(string outputPath, List<(string Name, byte[] Svg)> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var writer = new StreamWriter(outputPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await EmitFilePreambleAsync(writer, entries.Count).ConfigureAwait(false);

        var byLength = entries
            .GroupBy(static e => Encoding.UTF8.GetByteCount(e.Name))
            .OrderBy(static g => g.Key)
            .ToList();

        foreach (var group in byLength)
        {
            await writer.WriteLineAsync(string.Format(CultureInfo.InvariantCulture, "            case {0}: return TryGetLen{0}(name, out svg);", group.Key)).ConfigureAwait(false);
        }

        await writer.WriteLineAsync("            default: svg = default; return false;").ConfigureAwait(false);
        await writer.WriteLineAsync("        }").ConfigureAwait(false);
        await writer.WriteLineAsync("    }").ConfigureAwait(false);

        // Per-length methods. Within each, switch on the first byte before exact matching.
        foreach (var group in byLength)
        {
            var sortedGroup = group.OrderBy(static e => e.Name, StringComparer.Ordinal).ToList();
            await EmitLengthBucketAsync(writer, group.Key, sortedGroup).ConfigureAwait(false);
        }

        await writer.WriteLineAsync("}").ConfigureAwait(false);
    }

    /// <summary>Writes the fixed file preamble plus the top-level dispatch.</summary>
    /// <param name="writer">Target writer.</param>
    /// <param name="count">Total icon count.</param>
    /// <returns>Task tracking the write.</returns>
    private static async Task EmitFilePreambleAsync(TextWriter writer, int count)
    {
        await writer.WriteAsync("""
            // Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
            // Glenn Watson and Contributors licenses this file to you under the MIT license.
            // See the LICENSE file in the project root for full license information.

            // <auto-generated>
            // This file is regenerated by tools/NuStreamDocs.Icons.MaterialDesign.Regen.
            // Do not edit by hand — manual edits will be lost on the next regen.
            // </auto-generated>

            namespace NuStreamDocs.Icons.MaterialDesign;

            /// <summary>Generated MDI icon catalogue — bucket-by-length switch over the upstream <c>Templarian/MaterialDesign-SVG</c> set.</summary>
            """).ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await EmitSuppressionsAsync(writer).ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteAsync($$"""
            internal static class MdiIconData
            {
                /// <summary>Number of icons in the generated catalogue.</summary>
                public const int Count = {{count.ToString(CultureInfo.InvariantCulture)}};

                /// <summary>Tries to resolve <paramref name="name"/> to its UTF-8 SVG bytes.</summary>
                /// <param name="name">UTF-8 icon name (no <c>material-</c> prefix).</param>
                /// <param name="svg">UTF-8 SVG bytes on hit.</param>
                /// <returns>True when the icon is in the catalogue.</returns>
                public static bool TryGet(System.ReadOnlySpan<byte> name, out System.ReadOnlySpan<byte> svg)
                {
                    switch (name.Length)
                    {
            """).ConfigureAwait(false);
    }

    /// <summary>Writes one per-length lookup bucket.</summary>
    /// <param name="writer">Target writer.</param>
    /// <param name="length">UTF-8 byte length for the bucket.</param>
    /// <param name="sortedGroup">Entries in ordinal-name order.</param>
    /// <returns>Task tracking the write.</returns>
    private static async Task EmitLengthBucketAsync(TextWriter writer, int length, List<(string Name, byte[] Svg)> sortedGroup)
    {
        await writer.WriteAsync($$"""

                /// <summary>{{length}}-byte names ({{sortedGroup.Count}} entries).</summary>
                private static bool TryGetLen{{length}}(System.ReadOnlySpan<byte> name, out System.ReadOnlySpan<byte> svg)
                {
                    switch (name[0])
                    {
            """).ConfigureAwait(false);

        var firstIndex = 0;
        while (firstIndex < sortedGroup.Count)
        {
            var firstByte = (byte)sortedGroup[firstIndex].Name[0];
            var nextIndex = firstIndex + 1;
            while (nextIndex < sortedGroup.Count && sortedGroup[nextIndex].Name[0] == sortedGroup[firstIndex].Name[0])
            {
                nextIndex++;
            }

            await writer.WriteLineAsync(FormatInvariant("            case (byte)'{0}':", EscapeCharLiteral((char)firstByte))).ConfigureAwait(false);
            for (var i = firstIndex; i < nextIndex; i++)
            {
                var (entryName, svg) = sortedGroup[i];
                var nameLiteral = Utf8Literal(entryName);
                var svgLiteral = Utf8Literal(svg);
                await writer.WriteLineAsync(FormatInvariant("                if (name.SequenceEqual({0})) {{ svg = {1}; return true; }}", nameLiteral, svgLiteral)).ConfigureAwait(false);
            }

            await writer.WriteLineAsync("                break;").ConfigureAwait(false);
            firstIndex = nextIndex;
        }

        await writer.WriteAsync("""
                        default:
                            break;
                    }
                    svg = default; return false;
                }
            """).ConfigureAwait(false);
    }

    /// <summary>Emits the per-class <c>[SuppressMessage]</c> attributes covering the analyser noise that doesn't apply to a generated catalogue.</summary>
    /// <param name="writer">Source writer.</param>
    /// <returns>Task tracking the writes.</returns>
    private static async Task EmitSuppressionsAsync(TextWriter writer)
    {
        // Each suppression goes on its own multi-line attribute so individual lines stay under 200 chars.
        var rules = new (string Category, string Rule, string Reason)[]
        {
            (SonarCategory, "S1541:Methods should not be too complex", "Generated bucket dispatch."),
            (SonarCategory, "S138:Methods should not have too many lines", "Generated catalogue."),
            (SonarCategory, "S1067:Expressions should not be too complex", "Generated catalogue."),
            (SonarCategory, "S103:Lines should not be too long", "Generated catalogue — SVG path data is sometimes long."),
            ("Style", "IDE0010:Add missing cases", "Default arm covers unmatched lengths."),
            (StyleCopCategory, "SA1503:Braces should not be omitted", "Generated single-line if statements."),
            (StyleCopCategory, "SA1107:Code should not contain multiple statements on one line", "Generated single-line if statements."),
        };

        for (var i = 0; i < rules.Length; i++)
        {
            var (category, rule, reason) = rules[i];
            await writer.WriteLineAsync($$"""
                [System.Diagnostics.CodeAnalysis.SuppressMessage(
                    "{{category}}",
                    "{{rule}}",
                    Justification = "{{reason}}")]
                """).ConfigureAwait(false);
        }
    }

    /// <summary>Shorthand for <c>string.Format(CultureInfo.InvariantCulture, ...)</c>.</summary>
    /// <param name="format">Composite format string.</param>
    /// <param name="args">Arguments.</param>
    /// <returns>Formatted string.</returns>
    private static string FormatInvariant(string format, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);

    /// <summary>Escapes one char for inclusion inside a single-quoted C# char literal.</summary>
    /// <param name="value">Char to render.</param>
    /// <returns>Escaped source form without the surrounding quotes.</returns>
    private static string EscapeCharLiteral(char value) =>
        value switch
        {
            '\'' => "\\'",
            '\\' => "\\\\",
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

    /// <summary>Renders <paramref name="value"/> as a C# <c>"..."u8</c> literal — escapes non-printables and quotes.</summary>
    /// <param name="value">String value.</param>
    /// <returns>Source-form literal including the trailing <c>u8</c>.</returns>
    private static string Utf8Literal(string value) => Utf8Literal(Encoding.UTF8.GetBytes(value));

    /// <summary>Renders <paramref name="bytes"/> as a C# <c>"..."u8</c> literal — escapes non-printables and quotes.</summary>
    /// <param name="bytes">UTF-8 bytes.</param>
    /// <returns>Source-form literal including the trailing <c>u8</c>.</returns>
    private static string Utf8Literal(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length + 8);
        builder.Append('"');
        for (var i = 0; i < bytes.Length; i++)
        {
            AppendByte(builder, bytes[i]);
        }

        builder.Append("\"u8");
        return builder.ToString();
    }

    /// <summary>Appends one byte's source form to <paramref name="builder"/>.</summary>
    /// <param name="builder">Destination.</param>
    /// <param name="b">Byte to render.</param>
    private static void AppendByte(StringBuilder builder, byte b)
    {
        if (b is (byte)'"')
        {
            builder.Append("\\\"");
            return;
        }

        if (b is (byte)'\\')
        {
            builder.Append("\\\\");
            return;
        }

        if (b is (byte)'\n')
        {
            builder.Append("\\n");
            return;
        }

        if (b is (byte)'\r')
        {
            builder.Append("\\r");
            return;
        }

        if (b is (byte)'\t')
        {
            builder.Append("\\t");
            return;
        }

        if (b is >= FirstPrintableAsciiByte and <= LastPrintableAsciiByte)
        {
            builder.Append((char)b);
            return;
        }

        builder.Append('\\').Append('x').Append(b.ToString("X2", CultureInfo.InvariantCulture));
    }
}
