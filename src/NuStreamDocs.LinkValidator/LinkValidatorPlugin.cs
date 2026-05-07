// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.LinkValidator.Logging;
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
    : IBuildConfigurePlugin, IBuildFinalizePlugin
{
    /// <summary>Process exit code returned when at least one fatal diagnostic surfaces.</summary>
    private const int StrictFailureExitCode = 2;

    /// <summary>Configured options.</summary>
    private readonly LinkValidatorOptions _options;

    /// <summary>HTTP client factory; when null the plugin owns its own client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory;

    /// <summary>Logger captured at construction; defaults to <see cref="NullLogger.Instance"/> when no logger is supplied.</summary>
    private readonly ILogger _logger;

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
        // Validation runs at finalize against the on-disk output rather than during the
        // render-loop scan phase. The render loop only sees post-PostRender HTML — for any
        // page that hits the cross-page barrier (anything containing @autoref:, autorefs
        // resolution, or other late-rewrite markers), the scan-time HTML still has those
        // raw markers, so a Scan-time corpus would report every cross-page reference as
        // a broken link. Walking disk after PostResolve + write produces the canonical
        // post-everything HTML and matches what a browser sees.
        var corpus = await ValidationCorpus.BuildAsync(context.OutputRoot, _options.Parallelism, cancellationToken).ConfigureAwait(false);
        LastDiagnostics = await ValidateAsync(corpus, cancellationToken).ConfigureAwait(false);
        ReportThroughLogger(_logger, LastDiagnostics);

        if (!LinkValidatorReporter.HasFatal(LastDiagnostics))
        {
            return;
        }

        Environment.ExitCode = StrictFailureExitCode;
    }

    /// <summary>Builds the corpus from disk and runs both validators.</summary>
    /// <param name="outputRoot">Site output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined diagnostics, with severity demoted when the matching strict flag is off.</returns>
    public async Task<LinkDiagnostic[]> RunAsync(DirectoryPath outputRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot.Value);

        var corpus = await ValidationCorpus.BuildAsync(outputRoot, _options.Parallelism, cancellationToken).ConfigureAwait(false);
        return await ValidateAsync(corpus, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Routes each diagnostic through the configured logger so it picks up the host's formatter (ANSI tags, structured sinks).</summary>
    /// <param name="logger">Logger to receive diagnostics.</param>
    /// <param name="diagnostics">Diagnostics.</param>
    private static void ReportThroughLogger(ILogger logger, LinkDiagnostic[] diagnostics)
    {
        for (var i = 0; i < diagnostics.Length; i++)
        {
            var d = diagnostics[i];
            if (d.Severity == LinkSeverity.Error)
            {
                LinkValidatorLoggingHelper.LogBrokenLinkError(logger, d.SourcePage, d.Message);
            }
            else
            {
                LinkValidatorLoggingHelper.LogBrokenLinkWarning(logger, d.SourcePage, d.Message);
            }
        }
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
