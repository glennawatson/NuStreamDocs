// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Sqlite;

/// <summary>Provides the vendored <c>sql.js-httpvfs</c> runtime files from embedded resources.</summary>
internal static class SqliteAssets
{
    /// <summary>The pinned upstream sql.js-httpvfs version — kept in sync with the bytes under <c>Assets/</c>.</summary>
    public const string PinnedRuntimeVersion = "0.8.12";

    /// <summary>Embedded-resource identifier for the UMD entry bundle (<c>dist/index.js</c>).</summary>
    private const string LoaderResourceName = "NuStreamDocs.Search.Sqlite.Assets.sql.js-httpvfs.js";

    /// <summary>Embedded-resource identifier for the Web Worker bundle (<c>dist/sqlite.worker.js</c>).</summary>
    private const string WorkerResourceName = "NuStreamDocs.Search.Sqlite.Assets.sqlite.worker.js";

    /// <summary>Embedded-resource identifier for the sql.js WebAssembly binary (<c>dist/sql-wasm.wasm</c>).</summary>
    private const string WasmResourceName = "NuStreamDocs.Search.Sqlite.Assets.sql-wasm.wasm";

    /// <summary>Cached bytes of the loader bundle; lazily loaded on first read.</summary>
    private static byte[]? _loader;

    /// <summary>Cached bytes of the worker bundle; lazily loaded on first read.</summary>
    private static byte[]? _worker;

    /// <summary>Cached bytes of the WebAssembly binary; lazily loaded on first read.</summary>
    private static byte[]? _wasm;

    /// <summary>Gets the bytes of the sql.js-httpvfs UMD loader bundle.</summary>
    /// <returns>Bytes of <c>sql.js-httpvfs.js</c>.</returns>
    public static byte[] LoaderBytes() => _loader ??= ReadEmbeddedResource(LoaderResourceName);

    /// <summary>Gets the bytes of the sql.js-httpvfs Web Worker bundle.</summary>
    /// <returns>Bytes of <c>sqlite.worker.js</c>.</returns>
    public static byte[] WorkerBytes() => _worker ??= ReadEmbeddedResource(WorkerResourceName);

    /// <summary>Gets the bytes of the sql.js WebAssembly binary.</summary>
    /// <returns>Bytes of <c>sql-wasm.wasm</c>.</returns>
    public static byte[] WasmBytes() => _wasm ??= ReadEmbeddedResource(WasmResourceName);

    /// <summary>Reads <paramref name="name"/> from this assembly's manifest resources.</summary>
    /// <param name="name">Embedded-resource identifier.</param>
    /// <returns>Resource bytes.</returns>
    private static byte[] ReadEmbeddedResource(string name)
    {
        var asm = typeof(SqliteAssets).Assembly;
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(BuildResourceNotFoundMessage(asm, name));
        using MemoryStream sink = new();
        stream.CopyTo(sink);
        return sink.ToArray();
    }

    /// <summary>Composes the resource-not-found message via the project's <see cref="StringCompose"/> helper.</summary>
    /// <param name="asm">Assembly being inspected.</param>
    /// <param name="name">Missing resource identifier.</param>
    /// <returns>Composed message.</returns>
    private static string BuildResourceNotFoundMessage(System.Reflection.Assembly asm, string name)
    {
        var available = string.Join(", ", asm.GetManifestResourceNames());
        return StringCompose.Concat(
            "Embedded resource '",
            name,
            "' not found in ",
            asm.GetName().Name ?? "<null>",
            StringCompose.Concat(". Available: ", available));
    }
}
