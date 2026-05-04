// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Plugins;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Plugin that runs the C# reference generator before page discovery.
/// </summary>
/// <remarks>
/// <para>
/// In <see cref="CSharpApiGeneratorMode.EmitMarkdown"/> the plugin walks
/// the configured NuGet manifest during <see cref="OnConfigureAsync"/>
/// and writes Markdown into <c>docs/{OutputMarkdownSubdirectory}</c> —
/// by the time discovery runs the generated pages are on disk and look
/// like any author-written content.
/// </para>
/// <para>
/// In <see cref="CSharpApiGeneratorMode.Direct"/> the plugin walks the
/// same source via SourceDocParser's direct-extract API and stashes the
/// merged catalog on <see cref="LastExtraction"/> for downstream
/// renderers / tests / future virtual-page providers to consume — no
/// intermediate Markdown is written to disk.
/// </para>
/// </remarks>
public sealed class CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions options, ILogger logger) : IDocPlugin
{
    /// <summary>Configured options.</summary>
    private readonly CSharpApiGeneratorOptions _options = ValidateOptions(options);

    /// <summary>Optional logger handed to the pipeline.</summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Initializes a new instance of the <see cref="CSharpApiGeneratorPlugin"/> class.</summary>
    /// <param name="options">Generator options.</param>
    public CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Gets the most recent direct-extract result, or <c>null</c> when the plugin ran in <see cref="CSharpApiGeneratorMode.EmitMarkdown"/> or has not yet run.</summary>
    public DirectExtractionResult? LastExtraction { get; private set; }

    /// <inheritdoc/>
    public byte[] Name => "csharp-apigenerator"u8.ToArray();

    /// <inheritdoc/>
    public async ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        if (_options.Mode is CSharpApiGeneratorMode.Direct)
        {
            LastExtraction = await CSharpApiGenerator.ExtractAsync(_options, _logger, cancellationToken).ConfigureAwait(false);
            return;
        }

        await CSharpApiGenerator.GenerateAsync(_options, context.InputRoot, _logger, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static CSharpApiGeneratorOptions ValidateOptions(CSharpApiGeneratorOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        opts.Validate();
        return opts;
    }
}
