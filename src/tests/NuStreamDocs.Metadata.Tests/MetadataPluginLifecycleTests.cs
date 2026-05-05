// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Metadata.Tests;

/// <summary>Lifecycle method coverage for <c>MetadataPlugin</c>.</summary>
public class MetadataPluginLifecycleTests
{
    /// <summary>ConfigureAsync captures the input root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureAsync() =>
        await new MetadataPlugin().ConfigureAsync(new BuildConfigureContext("/in", "/out", [], new()), CancellationToken.None);

    /// <summary>PreRender passes the source through when no metadata is registered.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreRenderPassesThrough()
    {
        var sink = new ArrayBufferWriter<byte>(16);
        var ctx = new PagePreRenderContext("page.md", "hello"u8, sink);
        new MetadataPlugin().PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("hello");
    }
}
