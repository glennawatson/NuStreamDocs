// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Metadata;

/// <summary>
/// Plugin that inherits frontmatter from directory-level
/// <c>_meta.yml</c> files and per-page <c>page.md.meta.yml</c>
/// sidecars into each page's frontmatter, byte-level.
/// </summary>
/// <remarks>
/// <para>
/// Concept-level inspired by Statiq's directory / sidecar metadata
/// model — but the implementation here is byte-level UTF-8: walks
/// the input root once at <see cref="OnConfigureAsync"/> to build a
/// <see cref="MetadataRegistry"/>, then implements
/// <see cref="IMarkdownPreprocessor"/> so the per-page splice
/// happens in the same scratch-buffer pass as every other Markdown
/// preprocessor.
/// </para>
/// <para>
/// Precedence: page's own frontmatter > sidecar > closest ancestor
/// directory > further ancestor. Only keys absent from the page
/// itself are spliced; keys are appended in the page's frontmatter
/// region with no reformatting.
/// </para>
/// </remarks>
public sealed class MetadataPlugin(MetadataOptions options) : IDocPlugin, IMarkdownPreprocessor
{
    /// <summary>Configured options.</summary>
    private readonly MetadataOptions _options = ValidateOptions(options);

    /// <summary>Registry built at configure time; nullable until the first <see cref="OnConfigureAsync"/> completes.</summary>
    private MetadataRegistry _registry = MetadataRegistry.Empty;

    /// <summary>Initializes a new instance of the <see cref="MetadataPlugin"/> class with default options.</summary>
    public MetadataPlugin()
        : this(MetadataOptions.Default)
    {
    }

    /// <inheritdoc/>
    public string Name => "metadata";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _registry = MetadataCollector.Build(context.InputRoot, in _options);
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
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Path-blind callers (manual harnesses, tests against the no-arg overload)
        // get an unmodified pass-through; metadata splicing is keyed on the page's
        // relative path, which only the path-aware overload receives.
        var dst = writer.GetSpan(source.Length);
        source.CopyTo(dst);
        writer.Advance(source.Length);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        var extra = _registry.ExtraFor(relativePath);
        FrontmatterSplicer.Splice(source, extra, writer);
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static MetadataOptions ValidateOptions(MetadataOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        opts.Validate();
        return opts;
    }
}
