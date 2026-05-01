// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Metadata.Tests;

/// <summary>Lifecycle method coverage for <c>MetadataPlugin</c>.</summary>
public class MetadataPluginLifecycleTests
{
    /// <summary>OnConfigureAsync captures the input root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnConfigureAsync() => await new MetadataPlugin().OnConfigureAsync(new(default, "/in", "/out", []), CancellationToken.None);

    /// <summary>OnRenderPageAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderPageAsync()
    {
        var plugin = new MetadataPlugin();
        await plugin.OnRenderPageAsync(new("p.md", default, new(8)), CancellationToken.None);
    }

    /// <summary>OnFinaliseAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnFinaliseAsync() => await new MetadataPlugin().OnFinaliseAsync(new("/out"), CancellationToken.None);

    /// <summary>Preprocess (single-arg overload) passes the source through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessSingleArg()
    {
        var sink = new ArrayBufferWriter<byte>(16);
        new MetadataPlugin().Preprocess("hello"u8, sink);
        await Assert.That(System.Text.Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("hello");
    }

    /// <summary>Preprocess (path overload) also passes through when no metadata is registered.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessPathOverload()
    {
        var sink = new ArrayBufferWriter<byte>(16);
        new MetadataPlugin().Preprocess("hello"u8, sink, "page.md");
        await Assert.That(System.Text.Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("hello");
    }
}
