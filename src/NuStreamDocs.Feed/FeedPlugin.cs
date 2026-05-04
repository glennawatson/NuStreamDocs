// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Blog.Common;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Feed;

/// <summary>
/// Plugin that emits RSS / Atom feeds for the configured posts
/// directory. Generated files land under <c>{OutputRoot}/{OutputSubdirectory}/feed.xml</c>
/// (RSS) and <c>atom.xml</c> (Atom).
/// </summary>
/// <remarks>
/// Reuses <see cref="BlogPostScanner"/> as the post source so the
/// same authoring directory powers the rendered blog and the feeds.
/// Generation runs in <see cref="OnFinalizeAsync"/> so any blog plugin
/// that may have rewritten / appended posts has finished first.
/// </remarks>
public sealed class FeedPlugin(FeedOptions options, TimeProvider timeProvider, ILogger logger) : IDocPlugin
{
    /// <summary>Configured options.</summary>
    private readonly FeedOptions _options = ValidateOptions(options);

    /// <summary>Wall-clock provider; injected so tests can substitute a deterministic clock.</summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Captured input root from the configure phase; required by finalize.</summary>
    private DirectoryPath _inputRoot;

    /// <summary>Initializes a new instance of the <see cref="FeedPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public FeedPlugin(FeedOptions options)
        : this(options, TimeProvider.System, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FeedPlugin"/> class with a custom clock.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="timeProvider">Wall-clock provider.</param>
    public FeedPlugin(FeedOptions options, TimeProvider timeProvider)
        : this(options, timeProvider, NullLogger.Instance)
    {
    }

    /// <inheritdoc/>
    public string Name => "feed";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _inputRoot = context.InputRoot;
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
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_options.Formats == FeedFormats.None || _inputRoot.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        var postsRoot = Path.Combine(_inputRoot, _options.PostsSubdirectory);
        var posts = BlogPostScanner.Scan(postsRoot, _inputRoot);
        if (posts.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        var outputDir = Path.Combine(context.OutputRoot, _options.OutputSubdirectory);
        Directory.CreateDirectory(outputDir);
        var generatedAt = _timeProvider.GetUtcNow();
        FeedEmitter.WriteEnabledFormats(_options, outputDir, posts, generatedAt, _logger);

        return ValueTask.CompletedTask;
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static FeedOptions ValidateOptions(FeedOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        opts.Validate();
        return opts;
    }
}
