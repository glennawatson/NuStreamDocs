// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Blog.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Blog.MkDocs;

/// <summary>
/// mkdocs-material-style blog plugin: reads posts under
/// <c>{BlogSubdirectory}/posts/</c> and writes an index plus category
/// archives back into the docs tree before page discovery.
/// </summary>
public sealed class MkDocsBlogPlugin(MkDocsBlogOptions options, ILogger logger) : IBuildDiscoverPlugin
{
    /// <summary>Configured options.</summary>
    private readonly MkDocsBlogOptions _options = ValidateOptions(options);

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Initializes a new instance of the <see cref="MkDocsBlogPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public MkDocsBlogPlugin(MkDocsBlogOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "mkdocs-blog"u8;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => new(PluginBand.Earliest);

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        var blogRoot = context.InputRoot / _options.BlogSubdirectory;
        await BlogContentGenerator.GenerateAsync(
            _logger,
            new(
                PostsRoot: blogRoot / "posts",
                DocsRoot: context.InputRoot,
                IndexPath: blogRoot.File("index.md"),
                IndexTitle: _options.IndexTitle,
                EmitArchives: _options.EmitCategoryArchives,
                ArchiveRoot: blogRoot / "category",
                ArchiveFallbackSlug: [.. "category"u8]),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static MkDocsBlogOptions ValidateOptions(MkDocsBlogOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        opts.Validate();
        return opts;
    }
}
