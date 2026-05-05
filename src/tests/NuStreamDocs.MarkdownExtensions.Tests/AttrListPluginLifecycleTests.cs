// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.AttrList;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>PostRender coverage for <c>AttrListPlugin</c>.</summary>
public class AttrListPluginLifecycleTests
{
    /// <summary>PostRender rewrites <c>{ #id .class }</c> markup into HTML attributes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PostRenderRewrites()
    {
        var output = RunPostRender("<h1>Title</h1>\n{ #lead }\n"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("Title");
    }

    /// <summary>PostRender is a no-op when there's no AttrList markup.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PostRenderNoOp() =>
        await Assert.That(new AttrListPlugin().NeedsRewrite("<p>plain</p>"u8)).IsFalse();

    /// <summary>Drives one PostRender call against a fresh sink and returns the rewritten bytes.</summary>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rewritten output bytes.</returns>
    private static byte[] RunPostRender(ReadOnlySpan<byte> html)
    {
        ArrayBufferWriter<byte> output = new(64);
        PagePostRenderContext ctx = new("p.md", default, html, output);
        new AttrListPlugin().PostRender(in ctx);
        return [.. output.WrittenSpan];
    }
}
