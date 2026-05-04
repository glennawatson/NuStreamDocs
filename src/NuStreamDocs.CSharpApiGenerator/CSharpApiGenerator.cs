// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.CSharpApiGenerator.Logging;
using NuStreamDocs.Logging;
using SourceDocParser;
using SourceDocParser.Model;
using SourceDocParser.Zensical;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Static helpers that drive SourceDocParser against a configured
/// <see cref="CSharpApiGeneratorOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GenerateAsync"/> runs the emit pipeline and writes
/// Markdown into <c>{docsInputRoot}/{OutputMarkdownSubdirectory}</c>.
/// </para>
/// <para>
/// <see cref="ExtractAsync"/> runs SourceDocParser's direct-mode
/// extract and returns the merged catalog in memory, skipping disk
/// emission entirely.
/// </para>
/// <para>
/// Held outside the plugin class so callers (CLI verbs, tests) can
/// invoke generation without going through <see cref="CSharpApiGeneratorPlugin"/>'s
/// configure hook.
/// </para>
/// </remarks>
public static class CSharpApiGenerator
{
    /// <summary>Runs the emit-mode pipeline and writes Markdown into <paramref name="docsInputRoot"/>/<see cref="CSharpApiGeneratorOptions.OutputMarkdownSubdirectory"/>.</summary>
    /// <param name="options">Generator options.</param>
    /// <param name="docsInputRoot">Absolute path to the docs input root the build pipeline discovers.</param>
    /// <param name="logger">Optional logger; <see cref="NullLogger.Instance"/> by default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The absolute path to the directory that received the generated Markdown.</returns>
    public static async Task<string> GenerateAsync(
        CSharpApiGeneratorOptions options,
        string docsInputRoot,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(docsInputRoot);
        options.Validate();

        var outputRoot = Path.Combine(docsInputRoot, options.OutputMarkdownSubdirectory);
        Directory.CreateDirectory(outputRoot);

        var resolvedLogger = logger ?? NullLogger.Instance;
        var source = AssemblySourceFactory.Create(options.Inputs, resolvedLogger);
        using var sourceLifetime = source as IDisposable;

        await PhaseTimer.RunAsync(
            resolvedLogger,
            l => LogGeneratorStartIfEnabled(l, options, outputRoot),
            static (l, result, secs) => CSharpApiGeneratorLoggingHelper.LogGeneratorComplete(l, result.CanonicalTypes, result.PagesEmitted, secs),
            async () =>
            {
                var emitter = new ZensicalDocumentationEmitter();
                var extractor = new MetadataExtractor();
                return await extractor.RunAsync(source, outputRoot, emitter, resolvedLogger, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

        if (options.EmitIndexPage)
        {
            ApiIndexWriter.Write(outputRoot, options.IndexTitle.AsSpan(), options.IndexIntroduction.AsSpan());
        }

        return outputRoot;
    }

    /// <summary>Runs the direct-mode pipeline and returns the merged catalog without touching disk.</summary>
    /// <param name="options">Generator options; <see cref="CSharpApiGeneratorOptions.OutputMarkdownSubdirectory"/> is ignored in direct mode.</param>
    /// <param name="logger">Optional logger; <see cref="NullLogger.Instance"/> by default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The merged <see cref="DirectExtractionResult"/>.</returns>
    public static async Task<DirectExtractionResult> ExtractAsync(
        CSharpApiGeneratorOptions options,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var resolvedLogger = logger ?? NullLogger.Instance;
        var source = AssemblySourceFactory.Create(options.Inputs, resolvedLogger);
        using var sourceLifetime = source as IDisposable;

        return await PhaseTimer.RunAsync(
            resolvedLogger,
            l => LogDirectExtractStartIfEnabled(l, options),
            static (l, result, secs) => CSharpApiGeneratorLoggingHelper.LogDirectExtractComplete(l, result.CanonicalTypes.Length, result.SourceLinks.Length, secs),
            async () =>
            {
                var extractor = new MetadataExtractor();
                return await extractor.ExtractAsync(source, resolvedLogger, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    /// <summary>Renders <paramref name="inputs"/> as a short human-readable label for log lines.</summary>
    /// <param name="inputs">Input shapes.</param>
    /// <returns>Label.</returns>
    internal static string DescribeInputs(CSharpApiGeneratorInput[] inputs)
    {
        if (inputs.Length is 1)
        {
            return DescribeInput(inputs[0]);
        }

        var parts = new string[inputs.Length];
        for (var i = 0; i < inputs.Length; i++)
        {
            parts[i] = DescribeInput(inputs[i]);
        }

        return string.Join(',', parts);
    }

    /// <summary>Renders one input as a short human-readable label.</summary>
    /// <param name="input">Input shape.</param>
    /// <returns>Label.</returns>
    internal static string DescribeInput(CSharpApiGeneratorInput input) => input switch
    {
        NuGetManifestInput m => $"manifest:{m.RootDirectory}",
        NuGetPackagesInput p => $"packages:{p.Packages.Length}",
        LocalAssembliesInput l => $"assemblies:{l.AssemblyPaths.Length}@{l.Tfm}",
        CustomInput => "custom-source",
        _ => input.GetType().Name,
    };

    /// <summary>Emits the generator-start log only when the configured level is enabled, so <see cref="DescribeInputs"/>'s <c>string.Join</c> stays out of the cold path.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="options">Generator options used to describe the input shape.</param>
    /// <param name="outputRoot">Resolved output directory.</param>
    [SuppressMessage(
        "Performance",
        "CA1873:Avoid potentially expensive logging",
        Justification = "DescribeInputs is gated on logger.IsEnabled; analyzer doesn't see through the source-generated [LoggerMessage] partial.")]
    private static void LogGeneratorStartIfEnabled(ILogger logger, CSharpApiGeneratorOptions options, string outputRoot)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        CSharpApiGeneratorLoggingHelper.LogGeneratorStart(logger, DescribeInputs(options.Inputs), outputRoot);
    }

    /// <summary>Emits the direct-extract-start log only when the configured level is enabled.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="options">Generator options used to describe the input shape.</param>
    [SuppressMessage(
        "Performance",
        "CA1873:Avoid potentially expensive logging",
        Justification = "DescribeInputs is gated on logger.IsEnabled; analyzer doesn't see through the source-generated [LoggerMessage] partial.")]
    private static void LogDirectExtractStartIfEnabled(ILogger logger, CSharpApiGeneratorOptions options)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        CSharpApiGeneratorLoggingHelper.LogDirectExtractStart(logger, DescribeInputs(options.Inputs));
    }
}
