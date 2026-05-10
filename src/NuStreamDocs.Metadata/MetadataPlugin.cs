// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Metadata;

/// <summary>
/// Inherits frontmatter from directory-level <c>_meta.yml</c> files and per-page
/// <c>page.md.meta.yml</c> sidecars into each page's frontmatter. Precedence
/// (highest to lowest): page's own frontmatter, sidecar, closest ancestor directory,
/// further ancestors. Only keys absent from the page itself are spliced.
/// </summary>
public sealed class MetadataPlugin : IBuildConfigurePlugin, IPagePreRenderPlugin
{
    /// <summary>Configured options.</summary>
    private readonly MetadataOptions _options;

    /// <summary>Registry built at configure time.</summary>
    private MetadataRegistry _registry = MetadataRegistry.Empty;

    /// <summary>Initializes a new instance of the <see cref="MetadataPlugin"/> class with default options.</summary>
    public MetadataPlugin()
        : this(MetadataOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MetadataPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public MetadataPlugin(MetadataOptions options) => _options = ValidateOptions(options);

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "metadata"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _registry = MetadataCollector.Build(context.InputRoot, in _options);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => !ReferenceEquals(_registry, MetadataRegistry.Empty);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context)
    {
        var writer = context.Output;

        var relativePath = context.RelativePath;
        if (relativePath.IsEmpty)
        {
            // Path-blind callers (manual harnesses, tests with no source path) get
            // an unmodified pass-through; metadata splicing is keyed on the page's
            // relative path, so without one there's nothing to splice.
            var source = context.Source;
            var dst = writer.GetSpan(source.Length);
            source.CopyTo(dst);
            writer.Advance(source.Length);
            return;
        }

        var extra = _registry.ExtraFor(relativePath);
        FrontmatterSplicer.Splice(context.Source, extra, writer);
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static MetadataOptions ValidateOptions(MetadataOptions opts)
    {
        opts.Validate();
        return opts;
    }
}
