// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Search.Sqlite.Logging;

namespace NuStreamDocs.Search.Sqlite;

/// <summary>SQLite/FTS5 search-index plugin — emits a single <c>search.db</c> and the sql.js-httpvfs client glue.</summary>
public sealed class SqliteSearchPlugin : SearchPluginBase, IStaticAssetProvider
{
    /// <summary>Output path of the vendored sql.js-httpvfs UMD loader bundle.</summary>
    private static readonly FilePath LoaderPath = new("assets/javascripts/sql.js-httpvfs.js");

    /// <summary>Output path of the vendored sql.js-httpvfs Web Worker bundle.</summary>
    private static readonly FilePath WorkerPath = new("assets/javascripts/sqlite.worker.js");

    /// <summary>Output path of the vendored sql.js WebAssembly binary.</summary>
    private static readonly FilePath WasmPath = new("assets/javascripts/sql-wasm.wasm");

    /// <summary>Output path of the bind glue script.</summary>
    private static readonly FilePath BindScriptPath = new("assets/javascripts/sqlite-bind.js");

    /// <summary>UTF-8 head-extra snippet referencing the runtime loader and the deferred glue script.</summary>
    private static readonly byte[] HeadExtraBytes =
    [
        .. """
           <script src="/assets/javascripts/sql.js-httpvfs.js" defer></script>
           <script src="/assets/javascripts/sqlite-bind.js" defer></script>
           """u8
    ];

    /// <summary>Cached bind-script bytes.</summary>
    private static readonly byte[] BindScriptBytes = SqliteBindScript.Bytes.ToArray();

    /// <summary>Cached static-asset array surfaced via <see cref="IStaticAssetProvider"/>.</summary>
    private static readonly (FilePath Path, byte[] Bytes)[] StaticAssetSet =
    [
        (LoaderPath, SqliteAssets.LoaderBytes()),
        (WorkerPath, SqliteAssets.WorkerBytes()),
        (WasmPath, SqliteAssets.WasmBytes()),
        (BindScriptPath, BindScriptBytes)
    ];

    /// <summary>Captured option set.</summary>
    private readonly SqliteOptions _options;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="SqliteSearchPlugin"/> class with default options.</summary>
    public SqliteSearchPlugin()
        : this(SqliteOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SqliteSearchPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public SqliteSearchPlugin(in SqliteOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SqliteSearchPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public SqliteSearchPlugin(in SqliteOptions options, ILogger logger)
        : base(new SqliteEngine(options.ExcludePathPrefixes, options.IndexFullBody), logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>Gets the pinned upstream sql.js-httpvfs version the vendored runtime bundles.</summary>
    public static string PinnedRuntimeVersion => SqliteAssets.PinnedRuntimeVersion;

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
    protected override ValueTask OnIndexWrittenAsync(DirectoryPath siteRoot, CancellationToken cancellationToken)
    {
        var primary = PrimaryIndexPath;
        if (!primary.IsEmpty && primary.Exists() && _logger.IsEnabled(LogLevel.Information))
        {
            var path = primary.Value;
            var length = new FileInfo(path).Length;
            SqliteSearchLogging.LogDatabaseWritten(_logger, path, length);
        }

        _ = siteRoot;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void WriteEngineHeadExtra(IBufferWriter<byte> writer) => writer.Write(HeadExtraBytes);
}
