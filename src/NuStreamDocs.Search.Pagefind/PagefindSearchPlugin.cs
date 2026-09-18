// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Pagefind-format search-index plugin.</summary>
[System.Diagnostics.DebuggerDisplay("PagefindSearchPlugin: {StaticAssets}")]
public sealed class PagefindSearchPlugin : SearchPluginBase, IStaticAssetProvider
{
    /// <summary>Output path of the bind glue script.</summary>
    private static readonly FilePath BindScriptPath = new("assets/javascripts/pagefind-bind.js");

    /// <summary>Cached bind-script bytes.</summary>
    private static readonly byte[] BindScriptBytes = PagefindBindScript.Bytes.ToArray();

    /// <summary>Cached static-asset array surfaced via <see cref="IStaticAssetProvider"/>.</summary>
    private static readonly (FilePath Path, byte[] Bytes)[] StaticAssetSet = [(BindScriptPath, BindScriptBytes)];

    /// <summary>Captured option set.</summary>
    private readonly PagefindOptions _options;

    /// <summary>Logger captured for the post-write CLI invocation.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="PagefindSearchPlugin"/> class with default options.</summary>
    public PagefindSearchPlugin()
        : this(PagefindOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PagefindSearchPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public PagefindSearchPlugin(in PagefindOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PagefindSearchPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public PagefindSearchPlugin(in PagefindOptions options, ILogger logger)
        : base(PagefindEngine.Instance, logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets => StaticAssetSet;

    /// <inheritdoc/>
    protected override PathSegment OutputSubdirectory => _options.OutputSubdirectory;

    /// <inheritdoc/>
    protected override int MinTokenLength => _options.MinTokenLength;

    /// <inheritdoc/>
    protected override byte[][] SearchableFrontmatterKeys => _options.SearchableFrontmatterKeys;

    /// <inheritdoc/>
    protected override byte[] SectionPriorities => _options.SectionPriorities;

    /// <summary>Gets the head snippet referencing the Pagefind WASM loader and the glue script.</summary>
    /// <remarks>
    /// Both tags bypass Cloudflare Rocket Loader to preserve the module resolution context required by dynamic imports.
    /// </remarks>
    private static ReadOnlySpan<byte> HeadExtraBytes => """
           <script type="module" data-cfasync="false">import("/pagefind/pagefind.js").catch(()=>{});</script>
           <script src="/assets/javascripts/pagefind-bind.js" defer data-cfasync="false"></script>
           """u8;

    /// <inheritdoc/>
    protected override async ValueTask OnIndexWrittenAsync(DirectoryPath siteRoot, CancellationToken cancellationToken)
    {
        // Mark excluded pages first so the CLI skips them during indexing — keeps the fragment
        // count bounded on sites with very large auto-generated trees.
        await PagefindIgnoreInjector.InjectAsync(siteRoot, _options.ExcludePathPrefixes, _logger, cancellationToken)
            .ConfigureAwait(false);
        await PagefindCli.RunAsync(siteRoot, _options, _logger, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override void WriteEngineHeadExtra(IBufferWriter<byte> writer) => writer.Write(HeadExtraBytes);
}
