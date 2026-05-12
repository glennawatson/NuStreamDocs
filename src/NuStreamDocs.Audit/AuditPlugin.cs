// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Audit.Logging;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Audit;

/// <summary>Plugin that runs accessibility and performance lints over the rendered site at the end of a build.</summary>
public sealed class AuditPlugin
    : IBuildConfigurePlugin, IBuildFinalizePlugin
{
    /// <summary>Process exit code returned when strict mode is on and at least one finding surfaces.</summary>
    private const int StrictFailureExitCode = 2;

    /// <summary>Configured options.</summary>
    private readonly AuditOptions _options;

    /// <summary>Logger captured at construction; defaults to <see cref="NullLogger.Instance"/> when none is supplied.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="AuditPlugin"/> class with default options.</summary>
    public AuditPlugin()
        : this(AuditOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AuditPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public AuditPlugin(AuditOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AuditPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger that receives audit findings.</param>
    public AuditPlugin(AuditOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _logger = logger;
    }

    /// <summary>Gets the findings from the most recent run; intended for tests.</summary>
    public AuditDiagnostic[] LastDiagnostics { get; private set; } = [];

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "audit"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => new(PluginBand.Late);

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;

        LastDiagnostics = [];
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        if (context.OutputRoot.IsEmpty)
        {
            return;
        }

        // The audit runs at finalize against the on-disk output rather than during the render-loop
        // scan phase: only the post-everything HTML (after PostRender, PostResolve, and write)
        // matches what a browser actually sees.
        AuditLoggingHelper.LogAuditStart(_logger, context.OutputRoot.Value);
        var (diagnostics, pageCount) = await RunCoreAsync(context.OutputRoot, cancellationToken).ConfigureAwait(false);
        LastDiagnostics = diagnostics;

        for (var i = 0; i < diagnostics.Length; i++)
        {
            var d = diagnostics[i];
            AuditLoggingHelper.LogAuditFinding(_logger, d.Page, d.Rule, d.Message);
        }

        AuditLoggingHelper.LogAuditComplete(_logger, pageCount, diagnostics.Length);

        if (_options.Strict && diagnostics is [_, ..])
        {
            Environment.ExitCode = StrictFailureExitCode;
        }
    }

    /// <summary>Audits every HTML page under <paramref name="outputRoot"/> and returns the findings.</summary>
    /// <param name="outputRoot">Site output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The findings, ordered by page then lint.</returns>
    public async Task<AuditDiagnostic[]> RunAsync(DirectoryPath outputRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot.Value);
        var (diagnostics, _) = await RunCoreAsync(outputRoot, cancellationToken).ConfigureAwait(false);
        return diagnostics;
    }

    /// <summary>Orders findings by page URL, then by lint, for stable reporting.</summary>
    /// <param name="left">First finding.</param>
    /// <param name="right">Second finding.</param>
    /// <returns>A signed ordering value.</returns>
    private static int CompareDiagnostics(AuditDiagnostic left, AuditDiagnostic right)
    {
        var byPage = string.CompareOrdinal(left.Page, right.Page);
        return byPage != 0 ? byPage : ((int)left.Rule).CompareTo((int)right.Rule);
    }

    /// <summary>Yields every <c>.html</c> file under <paramref name="root"/>.</summary>
    /// <param name="root">Absolute site root.</param>
    /// <returns>Absolute paths.</returns>
    private static IEnumerable<FilePath> EnumerateHtml(DirectoryPath root)
    {
        // foreach over IEnumerable<string> from Directory.EnumerateFiles — no indexed alternative.
        foreach (var path in Directory.EnumerateFiles(root.Value, "*.html", SearchOption.AllDirectories))
        {
            yield return new(path);
        }
    }

    /// <summary>Walks the output, audits each page in parallel, and returns the sorted findings plus the page count.</summary>
    /// <param name="outputRoot">Site output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The findings and the number of pages audited.</returns>
    private async Task<(AuditDiagnostic[] Diagnostics, int PageCount)> RunCoreAsync(DirectoryPath outputRoot, CancellationToken cancellationToken)
    {
        DirectoryPath fullRoot = new(Path.GetFullPath(outputRoot.Value));
        if (!Directory.Exists(fullRoot.Value))
        {
            return ([], 0);
        }

        FilePath[] htmlFiles = [.. EnumerateHtml(fullRoot)];
        ConcurrentBag<AuditDiagnostic> findings = [];

        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.Parallelism
        };

        await Parallel.ForEachAsync(
            htmlFiles,
            parallelOptions,
            async (path, ct) =>
            {
                var bytes = await File.ReadAllBytesAsync(path.Value, ct).ConfigureAwait(false);
                UrlPath page = new(Path.GetRelativePath(fullRoot.Value, path.Value).Replace('\\', '/'));
                var pageFindings = PageAuditor.Audit(page, bytes, _options);
                for (var i = 0; i < pageFindings.Length; i++)
                {
                    findings.Add(pageFindings[i]);
                }
            }).ConfigureAwait(false);

        var ordered = findings.ToArray();
        Array.Sort(ordered, CompareDiagnostics);
        return (ordered, htmlFiles.Length);
    }
}
