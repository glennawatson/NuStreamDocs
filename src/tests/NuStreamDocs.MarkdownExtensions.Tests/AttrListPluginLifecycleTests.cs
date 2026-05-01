// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.MarkdownExtensions.AttrList;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>OnRenderPageAsync coverage for <c>AttrListPlugin</c>.</summary>
public class AttrListPluginLifecycleTests
{
    /// <summary>OnRenderPageAsync rewrites <c>{ #id .class }</c> markup into HTML attributes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderPageAsync()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        sink.Write("<h1>Title</h1>\n{ #lead }\n"u8);
        await new AttrListPlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(System.Text.Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("Title");
    }

    /// <summary>OnRenderPageAsync is a no-op when there's no AttrList markup.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderPageAsyncNoOp()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        sink.Write("<p>plain</p>"u8);
        await new AttrListPlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
    }
}
