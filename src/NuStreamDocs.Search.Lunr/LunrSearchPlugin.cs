// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Search.Lunr;

/// <summary>Lunr-format search-index plugin.</summary>
public sealed class LunrSearchPlugin : SearchPluginBase, IStaticAssetProvider
{
    /// <summary>Output path of the vendored Lunr runtime.</summary>
    private static readonly FilePath LunrRuntimePath = new("assets/javascripts/lunr.min.js");

    /// <summary>Output path of the bind glue script.</summary>
    private static readonly FilePath BindScriptPath = new("assets/javascripts/lunr-bind.js");

    /// <summary>UTF-8 head-extra snippet referencing the Lunr runtime and the deferred glue script.</summary>
    private static readonly byte[] HeadExtraBytes =
        [.. """
<script src="/assets/javascripts/lunr.min.js" defer></script>
<script src="/assets/javascripts/lunr-bind.js" defer></script>
"""u8];

    /// <summary>Cached bind-script bytes.</summary>
    private static readonly byte[] BindScriptBytes = LunrBindScript.Bytes.ToArray();

    /// <summary>Cached static-asset array surfaced via <see cref="IStaticAssetProvider"/>.</summary>
    private static readonly (FilePath Path, byte[] Bytes)[] StaticAssetSet =
    [
        (LunrRuntimePath, LunrAssets.LunrMinJsBytes()),
        (BindScriptPath, BindScriptBytes),
    ];

    /// <summary>Captured option set.</summary>
    private readonly LunrOptions _options;

    /// <summary>Initializes a new instance of the <see cref="LunrSearchPlugin"/> class with default options.</summary>
    public LunrSearchPlugin()
        : this(LunrOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LunrSearchPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public LunrSearchPlugin(in LunrOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LunrSearchPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public LunrSearchPlugin(in LunrOptions options, ILogger logger)
        : base(new LunrEngine(options.Language ?? [], options.ExtraStopwords), logger)
    {
        _options = options;
    }

    /// <summary>Gets the pinned upstream Lunr.js version of the vendored bundle this plugin ships.</summary>
    public static string PinnedRuntimeVersion => LunrAssets.PinnedVersion;

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

    /// <inheritdoc/>
    protected override async ValueTask OnIndexWrittenAsync(DirectoryPath siteRoot, CancellationToken cancellationToken)
    {
        var primary = PrimaryIndexPath;
        if (_options.Compression is SearchCompression.None || primary.IsEmpty || !primary.Exists())
        {
            return;
        }

        var raw = await File.ReadAllBytesAsync(primary.Value, cancellationToken).ConfigureAwait(false);
        await WriteGzipAsync(primary.Value + ".gz", raw, cancellationToken).ConfigureAwait(false);
        if (_options.Compression is SearchCompression.Smallest)
        {
            await WriteBrotliAsync(primary.Value + ".br", raw, cancellationToken).ConfigureAwait(false);
        }

        _ = siteRoot;
    }

    /// <inheritdoc/>
    protected override void WriteEngineHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(HeadExtraBytes);
    }

    /// <summary>Writes <paramref name="raw"/> through <see cref="GZipStream"/> at the smallest compression level.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="raw">Bytes to compress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    private static async Task WriteGzipAsync(string path, byte[] raw, CancellationToken cancellationToken)
    {
        await using var output = File.Create(path);
        await using GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: false);
        await gzip.WriteAsync(raw, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes <paramref name="raw"/> through <see cref="BrotliStream"/> at the smallest compression level.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="raw">Bytes to compress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    private static async Task WriteBrotliAsync(string path, byte[] raw, CancellationToken cancellationToken)
    {
        await using var output = File.Create(path);
        await using BrotliStream brotli = new(output, CompressionLevel.SmallestSize, leaveOpen: false);
        await brotli.WriteAsync(raw, cancellationToken).ConfigureAwait(false);
    }
}
