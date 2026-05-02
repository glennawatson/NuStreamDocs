// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Blog.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Blog;

/// <summary>
/// Plugin that scans a Wyam-style flat blog directory and writes the
/// generated index + tag archives back into the docs tree before
/// page discovery.
/// </summary>
/// <remarks>
/// Generation runs during <see cref="OnConfigureAsync"/>: by the time the
/// build pipeline begins discovery, <c>{PostsSubdirectory}/index.md</c>
/// and any <c>{PostsSubdirectory}/tags/{tag}.md</c> files are on disk
/// so they're picked up like author-written pages. The plugin does
/// not move the post files themselves — they stay flat under the
/// configured subdirectory and are rendered as ordinary pages.
/// </remarks>
public sealed class WyamBlogPlugin(WyamBlogOptions options, ILogger logger) : IDocPlugin
{
    /// <summary>Configured options.</summary>
    private readonly WyamBlogOptions _options = ValidateOptions(options);

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Initializes a new instance of the <see cref="WyamBlogPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public WyamBlogPlugin(WyamBlogOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <inheritdoc/>
    public string Name => "wyam-blog";

    /// <inheritdoc/>
    public async ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        var postsRoot = Path.Combine(context.InputRoot, _options.PostsSubdirectory);
        await BlogContentGenerator.GenerateAsync(
            _logger,
            new(
                PostsRoot: postsRoot,
                DocsRoot: context.InputRoot,
                IndexPath: Path.Combine(postsRoot, "index.md"),
                IndexTitle: _options.IndexTitle,
                EmitArchives: _options.EmitTagArchives,
                ArchiveRoot: Path.Combine(postsRoot, "tags"),
                ArchiveFallbackSlug: "tag"),
            cancellationToken).ConfigureAwait(false);
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
    private static WyamBlogOptions ValidateOptions(WyamBlogOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        opts.Validate();
        return opts;
    }
}
