// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Pagefind-format search-index plugin.</summary>
/// <remarks>
/// <para>
/// Themes contribute only the search-shell markup (<c>data-md-component</c> hooks); this plugin
/// ships everything else. At finalize, it invokes the bundled Pagefind CLI (<see cref="PagefindCli"/>)
/// against the rendered output, which produces the WASM runtime + binary inverted-index shards under
/// <c>&lt;site&gt;/pagefind/</c>. The plugin then drops the bind-glue script next to the runtime via
/// <see cref="IStaticAssetProvider"/> and emits the corresponding <c>&lt;script&gt;</c> tags through
/// <see cref="IHeadExtraProvider"/>.
/// </para>
/// </remarks>
public sealed class PagefindSearchPlugin : SearchPluginBase, IStaticAssetProvider
{
    /// <summary>Forward-slash relative path the bind glue is written to in the rendered output.</summary>
    private static readonly FilePath BindScriptPath = new("assets/javascripts/pagefind-bind.js");

    /// <summary>UTF-8 head-link snippet — the Pagefind WASM loader as an ES module followed by the glue script.</summary>
    /// <remarks>
    /// The loader is written as a module so Pagefind's <c>import.meta.url</c>-based asset resolution finds the
    /// sibling <c>.wasm</c> + <c>.pagefind</c> shards relative to <c>/pagefind/pagefind.js</c>. The glue script
    /// runs deferred so the DOM is ready when it queries the search-shell hooks.
    /// </remarks>
    private static readonly byte[] HeadExtraBytes =
        [.. """
<script type="module">import("/pagefind/pagefind.js").catch(()=>{});</script>
<script src="/assets/javascripts/pagefind-bind.js" defer></script>
"""u8];

    /// <summary>Captured option set; mirrored into the protected base properties.</summary>
    private readonly PagefindOptions _options;

    /// <summary>Logger captured for the post-write CLI invocation; stored here because the base type holds it privately.</summary>
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
    public (FilePath Path, byte[] Bytes)[] StaticAssets => [(BindScriptPath, PagefindBindScript.Bytes.ToArray())];

    /// <inheritdoc/>
    protected override PathSegment OutputSubdirectory => _options.OutputSubdirectory;

    /// <inheritdoc/>
    protected override int MinTokenLength => _options.MinTokenLength;

    /// <inheritdoc/>
    protected override byte[][] SearchableFrontmatterKeys => _options.SearchableFrontmatterKeys;

    /// <inheritdoc/>
    protected override byte[] SectionPriorities => _options.SectionPriorities;

    /// <inheritdoc/>
    protected override async ValueTask OnIndexWrittenAsync(DirectoryPath siteRoot, CancellationToken cancellationToken)
    {
        await PagefindCli.RunAsync(siteRoot, _options, _logger, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override void WriteEngineHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(HeadExtraBytes);
    }
}
