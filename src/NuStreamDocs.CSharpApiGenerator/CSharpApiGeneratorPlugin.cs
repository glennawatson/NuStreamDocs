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
/// In <see cref="CSharpApiGeneratorMode.EmitMarkdown"/> mode the plugin writes Markdown
/// into <c>docs/{OutputMarkdownSubdirectory}</c>; in <see cref="CSharpApiGeneratorMode.Direct"/>
/// mode it stashes the merged catalog on <see cref="LastExtraction"/> without writing files.
/// </remarks>
public sealed class CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions options, ILogger logger) : IBuildDiscoverPlugin
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
    public ReadOnlySpan<byte> Name => "csharp-apigenerator"u8;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => new(PluginBand.Earliest);

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        if (_options.Mode is CSharpApiGeneratorMode.Direct)
        {
            LastExtraction = await CSharpApiGenerator.ExtractAsync(_options, _logger, cancellationToken).ConfigureAwait(false);
            return;
        }

        await CSharpApiGenerator.GenerateAsync(_options, context.InputRoot, _logger, cancellationToken).ConfigureAwait(false);
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
