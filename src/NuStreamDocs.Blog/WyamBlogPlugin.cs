// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Blog.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Blog;

/// <summary>
/// Plugin that scans a Wyam-style flat blog directory and registers the
/// generated index plus tag archives as synthetic pages on the build context
/// before page discovery — nothing lands on disk in the source tree.
/// </summary>
public sealed class WyamBlogPlugin(WyamBlogOptions options, ILogger logger) : IBuildDiscoverPlugin
{
    /// <summary>Configured options.</summary>
    private readonly WyamBlogOptions _options = ValidateOptions(options);

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger = logger;

    /// <summary>Initializes a new instance of the <see cref="WyamBlogPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public WyamBlogPlugin(WyamBlogOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "wyam-blog"u8;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => new(PluginBand.Earliest);

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        var postsRoot = context.InputRoot / _options.PostsSubdirectory;
        await BlogContentGenerator.GenerateAsync(
            _logger,
            new(
                PostsRoot: postsRoot,
                DocsRoot: context.InputRoot,
                IndexPath: postsRoot.File("index.md"),
                IndexTitle: _options.IndexTitle,
                EmitArchives: _options.EmitTagArchives,
                ArchiveRoot: postsRoot / "tags",
                ArchiveFallbackSlug: [.. "tag"u8]),
            context.SyntheticPages,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static WyamBlogOptions ValidateOptions(WyamBlogOptions opts)
    {
        opts.Validate();
        return opts;
    }
}
