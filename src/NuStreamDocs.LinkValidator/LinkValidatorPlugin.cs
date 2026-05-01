// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.LinkValidator.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Plugin that runs the link validator at build finalise. Builds a
/// single corpus in one parallel pass, then runs both validators
/// against it.
/// </summary>
/// <remarks>
/// Mirror of the rxui website's source-link validator slot; takes
/// <see cref="LinkValidatorOptions"/> and writes diagnostic lines to
/// stderr. Fails the process via the captured exit code only when
/// the relevant strict flag is on, matching mkdocs' behaviour.
/// </remarks>
public sealed class LinkValidatorPlugin(LinkValidatorOptions options, Func<HttpClient>? httpClientFactory, ILogger logger) : IDocPlugin
{
    /// <summary>Process exit code returned when at least one fatal diagnostic surfaces.</summary>
    private const int StrictFailureExitCode = 2;

    /// <summary>Configured options.</summary>
    private readonly LinkValidatorOptions _options = ValidateOptions(options);

    /// <summary>HTTP client factory; when null the plugin owns its own client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory = httpClientFactory;

    /// <summary>Logger captured at construction; defaults to <see cref="NullLogger.Instance"/> when no logger is supplied.</summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

    /// <summary>Gets the diagnostics from the most recent run; intended for tests.</summary>
    public LinkDiagnostic[] LastDiagnostics { get; private set; } = [];

    /// <inheritdoc/>
    public string Name => "link-validator";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask OnFinaliseAsync(PluginFinaliseContext context, CancellationToken cancellationToken)
    {
        LastDiagnostics = await RunAsync(context.OutputRoot, cancellationToken).ConfigureAwait(false);
        ReportToConsole(LastDiagnostics);

        if (!LinkValidatorReporter.HasFatal(LastDiagnostics))
        {
            return;
        }

        Environment.ExitCode = StrictFailureExitCode;
    }

    /// <summary>Runs the corpus build + both validators.</summary>
    /// <param name="outputRoot">Site output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined diagnostics, with severity demoted when the matching strict flag is off.</returns>
    public async Task<LinkDiagnostic[]> RunAsync(string outputRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot);

        var stopwatch = Stopwatch.StartNew();
        var corpus = await ValidationCorpus.BuildAsync(outputRoot, _options.Parallelism, cancellationToken).ConfigureAwait(false);

        var (internalLinkCount, externalLinkCount) = LinkValidatorReporter.CountLinks(corpus);
        LinkValidatorLoggingHelper.LogValidationStart(_logger, outputRoot, corpus.Pages.Length, internalLinkCount, externalLinkCount);

        var internalDiags = await InternalLinkValidator.ValidateAsync(corpus, _options.Parallelism, cancellationToken).ConfigureAwait(false);
        var externalDiags = _options.StrictExternal
            ? await RunExternalAsync(corpus, cancellationToken).ConfigureAwait(false)
            : [];

        var merged = LinkValidatorReporter.Merge(internalDiags, externalDiags, _options.StrictInternal, _options.StrictExternal);
        var (brokenCount, warningCount) = LinkValidatorReporter.Tally(merged);

        for (var i = 0; i < merged.Length; i++)
        {
            var d = merged[i];
            LinkValidatorLoggingHelper.LogBrokenLink(_logger, d.Severity, d.SourcePage, d.Message);
        }

        stopwatch.Stop();
        LinkValidatorLoggingHelper.LogValidationComplete(_logger, brokenCount, warningCount, stopwatch.ElapsedMilliseconds);
        return merged;
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static LinkValidatorOptions ValidateOptions(LinkValidatorOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        opts.Validate();
        return opts;
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

        using var owned = new HttpClient();
        return await ExternalLinkValidator.ValidateAsync(corpus, _options.External, owned, cancellationToken).ConfigureAwait(false);
    }
}
