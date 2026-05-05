// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.LinkValidator.Logging;
using NuStreamDocs.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Plugin that validates internal and external links across the
/// rendered site. Pages are captured during the per-page scan phase;
/// validation runs once during the cross-page resolve barrier against
/// the accumulated corpus.
/// </summary>
/// <remarks>
/// Mirror of the rxui website's source-link validator slot; takes
/// <see cref="LinkValidatorOptions"/> and writes diagnostic lines to
/// stderr. Fails the process via the captured exit code only when
/// the relevant strict flag is on, matching mkdocs' behavior.
/// </remarks>
public sealed class LinkValidatorPlugin
    : IBuildConfigurePlugin, IPageScanPlugin, IBuildResolvePlugin
{
    /// <summary>Process exit code returned when at least one fatal diagnostic surfaces.</summary>
    private const int StrictFailureExitCode = 2;

    /// <summary>Configured options.</summary>
    private readonly LinkValidatorOptions _options;

    /// <summary>HTTP client factory; when null the plugin owns its own client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory;

    /// <summary>Logger captured at construction; defaults to <see cref="NullLogger.Instance"/> when no logger is supplied.</summary>
    private readonly ILogger _logger;

    /// <summary>Per-build accumulator filled during <see cref="Scan"/> and drained during <see cref="ResolveAsync"/>.</summary>
    private ConcurrentDictionary<byte[], PageLinks> _pages = new(ByteArrayComparer.Instance);

    /// <summary>Initializes a new instance of the <see cref="LinkValidatorPlugin"/> class with default options.</summary>
    public LinkValidatorPlugin()
        : this(LinkValidatorOptions.Default, httpClientFactory: null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LinkValidatorPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public LinkValidatorPlugin(LinkValidatorOptions options)
        : this(options, httpClientFactory: null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LinkValidatorPlugin"/> class with a custom HTTP factory.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client used by the external validator. When null the plugin builds a default client.</param>
    public LinkValidatorPlugin(LinkValidatorOptions options, Func<HttpClient>? httpClientFactory)
        : this(options, httpClientFactory, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LinkValidatorPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client used by the external validator. When null the plugin builds a default client.</param>
    /// <param name="logger">Logger that receives validation diagnostics.</param>
    public LinkValidatorPlugin(LinkValidatorOptions options, Func<HttpClient>? httpClientFactory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        options.Validate();
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>Gets the diagnostics from the most recent run; intended for tests.</summary>
    public LinkDiagnostic[] LastDiagnostics { get; private set; } = [];

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "link-validator"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority ScanPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority ResolvePriority => new(PluginBand.Late);

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;

        // Watch-mode rebuilds reuse the plugin instance — drop the previous build's pages.
        _pages = new(ByteArrayComparer.Instance);
        LastDiagnostics = [];
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Scan(in PageScanContext context)
    {
        var pageUrl = ToPageUrlBytes(context.RelativePath);
        _pages[pageUrl] = ValidationCorpus.Scan(pageUrl, context.Html);
    }

    /// <inheritdoc/>
    public async ValueTask ResolveAsync(BuildResolveContext context, CancellationToken cancellationToken)
    {
        _ = context;
        LastDiagnostics = await RunAsync(cancellationToken).ConfigureAwait(false);
        ReportToConsole(LastDiagnostics);

        if (!LinkValidatorReporter.HasFatal(LastDiagnostics))
        {
            return;
        }

        Environment.ExitCode = StrictFailureExitCode;
    }

    /// <summary>Validates the corpus accumulated by <see cref="Scan"/> and returns the merged diagnostics.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined diagnostics, with severity demoted when the matching strict flag is off.</returns>
    public async Task<LinkDiagnostic[]> RunAsync(CancellationToken cancellationToken)
    {
        var merged = await PhaseTimer.RunAsync(
            _logger,
            l => LinkValidatorLoggingHelper.LogValidationStart(l, "<in-memory>"),
            static (l, m, secs) =>
            {
                var (broken, warning) = LinkValidatorReporter.Tally(m);
                LinkValidatorLoggingHelper.LogValidationComplete(l, broken, warning, secs);
            },
            () => RunValidationAsync(cancellationToken)).ConfigureAwait(false);

        for (var i = 0; i < merged.Length; i++)
        {
            var d = merged[i];
            LinkValidatorLoggingHelper.LogBrokenLink(_logger, d.Severity, d.SourcePage, d.Message);
        }

        return merged;
    }

    /// <summary>Builds the corpus from disk (test/diagnostic boundary) and runs both validators.</summary>
    /// <param name="outputRoot">Site output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined diagnostics, with severity demoted when the matching strict flag is off.</returns>
    public async Task<LinkDiagnostic[]> RunAsync(DirectoryPath outputRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot.Value);

        var corpus = await ValidationCorpus.BuildAsync(outputRoot, _options.Parallelism, cancellationToken).ConfigureAwait(false);
        return await ValidateAsync(corpus, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Maps a source-relative path to its served URL bytes (replaces <c>.md</c> with <c>.html</c>).</summary>
    /// <param name="relativePath">Source-relative markdown path, forward-slashed.</param>
    /// <returns>UTF-8 page-relative URL bytes.</returns>
    private static byte[] ToPageUrlBytes(FilePath relativePath)
    {
        var path = relativePath.AsSpan();
        var hasMd = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var sourceChars = hasMd ? path[..^3] : path;
        var suffix = hasMd ? ".html"u8 : default;

        var size = Encoding.UTF8.GetByteCount(sourceChars) + suffix.Length;
        var result = new byte[size];
        var written = Encoding.UTF8.GetBytes(sourceChars, result);
        suffix.CopyTo(result.AsSpan(written));
        return result;
    }

    /// <summary>Writes the diagnostics to stderr.</summary>
    /// <param name="diagnostics">Diagnostics.</param>
    private static void ReportToConsole(LinkDiagnostic[] diagnostics)
    {
        for (var i = 0; i < diagnostics.Length; i++)
        {
            var d = diagnostics[i];
            Console.Error.WriteLine($"[{d.Severity}] {d.SourcePage}: {d.Message}");
        }
    }

    /// <summary>Builds the corpus from the accumulated pages and runs validation.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The merged diagnostic array.</returns>
    private async ValueTask<LinkDiagnostic[]> RunValidationAsync(CancellationToken cancellationToken)
    {
        var corpus = ValidationCorpus.FromPages(_pages);
        return await ValidateAsync(corpus, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Logs the corpus summary and runs the internal + external validators against <paramref name="corpus"/>.</summary>
    /// <param name="corpus">Validation corpus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The merged diagnostic array.</returns>
    private async Task<LinkDiagnostic[]> ValidateAsync(ValidationCorpus corpus, CancellationToken cancellationToken)
    {
        var (internalLinkCount, externalLinkCount) = LinkValidatorReporter.CountLinks(corpus);
        LinkValidatorLoggingHelper.LogValidationCorpus(_logger, corpus.Pages.Length, internalLinkCount, externalLinkCount);

        var internalDiags = await InternalLinkValidator.ValidateAsync(corpus, _options.Parallelism, cancellationToken).ConfigureAwait(false);
        var externalDiags = _options.StrictExternal
            ? await RunExternalAsync(corpus, cancellationToken).ConfigureAwait(false)
            : [];

        return LinkValidatorReporter.Merge(internalDiags, externalDiags, _options.StrictInternal, _options.StrictExternal);
    }

    /// <summary>Runs the external HTTP checker.</summary>
    /// <param name="corpus">Corpus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>External diagnostics.</returns>
    private async Task<LinkDiagnostic[]> RunExternalAsync(ValidationCorpus corpus, CancellationToken cancellationToken)
    {
        if (_httpClientFactory is not null)
        {
            var client = _httpClientFactory();
            return await ExternalLinkValidator.ValidateAsync(corpus, _options.External, client, cancellationToken).ConfigureAwait(false);
        }

        using SocketsHttpHandler handler = new()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15)
        };
        using HttpClient owned = new(handler, disposeHandler: false);
        return await ExternalLinkValidator.ValidateAsync(corpus, _options.External, owned, cancellationToken).ConfigureAwait(false);
    }
}
