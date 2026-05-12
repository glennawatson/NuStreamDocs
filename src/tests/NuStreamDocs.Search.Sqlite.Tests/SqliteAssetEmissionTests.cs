// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Sqlite.Tests;

/// <summary>Confirms the vendored runtime resources are embedded and readable.</summary>
public class SqliteAssetEmissionTests
{
    /// <summary>The loader bundle, worker bundle, and wasm binary are all non-empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task VendoredRuntimeResourcesPresent()
    {
        await Assert.That(SqliteAssets.LoaderBytes().Length).IsGreaterThan(0);
        await Assert.That(SqliteAssets.WorkerBytes().Length).IsGreaterThan(0);
        await Assert.That(SqliteAssets.WasmBytes().Length).IsGreaterThan(100_000);
    }

    /// <summary>The pinned runtime version constant is set.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PinnedRuntimeVersionIsSet()
    {
        await Assert.That(SqliteAssets.PinnedRuntimeVersion).IsNotNull();
        await Assert.That(SqliteAssets.PinnedRuntimeVersion).IsNotEmpty();
    }
}
