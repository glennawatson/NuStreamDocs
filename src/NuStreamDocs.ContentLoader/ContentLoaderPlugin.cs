// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.ContentLoader.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader;

/// <summary>
/// Discovery-phase plugin that runs each registered <see cref="IContentLoader"/> and feeds the pages
/// it produces into the build's synthetic-page sink so they render alongside disk-loaded Markdown.
/// </summary>
public sealed class ContentLoaderPlugin
    : IBuildDiscoverPlugin
{
    /// <summary>The loaders to run, in registration order.</summary>
    private readonly IContentLoader[] _loaders;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="ContentLoaderPlugin"/> class.</summary>
    /// <param name="loaders">The loaders to run.</param>
    public ContentLoaderPlugin(IContentLoader[] loaders)
        : this(loaders, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ContentLoaderPlugin"/> class.</summary>
    /// <param name="loaders">The loaders to run.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public ContentLoaderPlugin(IContentLoader[] loaders, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(loaders);
        _loaders = loaders;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "content-loader"u8;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        ContentLoaderContext loaderContext = new(context.InputRoot) { UseDirectoryUrls = context.UseDirectoryUrls };
        for (var i = 0; i < _loaders.Length; i++)
        {
            var loader = _loaders[i];
            var pages = await loader.LoadAsync(loaderContext, cancellationToken).ConfigureAwait(false);
            for (var j = 0; j < pages.Length; j++)
            {
                context.SyntheticPages.Add(pages[j]);
            }

            var loaderName = Encoding.UTF8.GetString(loader.Name);
            ContentLoaderLoggingHelper.LogLoaderProduced(_logger, loaderName, pages.Length);
        }
    }
}
