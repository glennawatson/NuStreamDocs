// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Search.Tests;

/// <summary>Coverage for SearchPlugin.Name + WriteHeadExtra + DocumentsSnapshot.</summary>
public class SearchPluginCoverageTests
{
    /// <summary>Name returns "search".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        SearchPlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("search"u8)).IsTrue();
    }

    /// <summary>WriteHeadExtra emits some bytes for the default options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtra()
    {
        SearchPlugin plugin = new();
        ArrayBufferWriter<byte> sink = new(64);
        plugin.WriteHeadExtra(sink);
        await Assert.That(sink.WrittenCount).IsGreaterThan(0);
    }

    /// <summary>DocumentsSnapshot is empty before any pages are rendered.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DocumentsSnapshotEmpty()
    {
        SearchPlugin plugin = new();
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }
}
