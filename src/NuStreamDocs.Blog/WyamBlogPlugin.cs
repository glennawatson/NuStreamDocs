// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Blog.Common;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Blog;

/// <summary>
/// Plugin that scans a Wyam-style flat blog directory and registers the
/// generated index plus tag archives as synthetic pages on the build context
/// before page discovery — nothing lands on disk in the source tree.
/// </summary>
public sealed class WyamBlogPlugin(WyamBlogOptions options, ILogger logger)
    : IBuildDiscoverPlugin, ISyntheticNavProvider
{
    /// <summary>Configured options.</summary>
    private readonly WyamBlogOptions _options = ValidateOptions(options);

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger = logger;

    /// <summary>Backing store for <see cref="SyntheticNavEntries"/>; populated synchronously in <see cref="DiscoverAsync"/>.</summary>
    private SyntheticNavEntry[] _navEntries = [];

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
    public IReadOnlyList<SyntheticNavEntry> SyntheticNavEntries => _navEntries;

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        // Publish the blog index's nav metadata up front (title/order come from the options) so the
        // section is anchored even if generation fails; the per-post entries (publish-date order)
        // come back from the generator and are merged onto the disk page nodes by the nav grafter.
        var indexEntry =
            new SyntheticNavEntry(
                DirectoryPath.FromString(_options.PostsSubdirectory).UrlJoin((UrlPath)"index.md"),
                _options.IndexTitle,
                _options.NavOrder,
                false);
        _navEntries = [indexEntry];

        var postsRoot = context.InputRoot / _options.PostsSubdirectory;
        var postEntries = await BlogContentGenerator.GenerateAsync(
            _logger,
            new(
                postsRoot,
                context.InputRoot,
                postsRoot.File("index.md"),
                _options.IndexTitle,
                _options.EmitTagArchives,
                postsRoot / "tags",
                [.. "tag"u8]),
            context.SyntheticPages,
            cancellationToken).ConfigureAwait(false);

        _navEntries = [indexEntry, .. postEntries];
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
