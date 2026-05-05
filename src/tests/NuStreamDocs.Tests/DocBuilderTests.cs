// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Tests;

/// <summary>Tests for <c>DocBuilder</c> plugin registration + render.</summary>
public class DocBuilderTests
{
    /// <summary>UsePlugin&lt;T&gt; should construct via the parameterless ctor and fire the page hook.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UsePluginGenericFiresRenderHook()
    {
        var builder = new DocBuilder().UsePlugin<RecordingPlugin>();
        ArrayBufferWriter<byte> html = new();
        await builder.RenderPageAsync("intro.md", new([.. "# Hi"u8]), html, CancellationToken.None);

        var output = Encoding.UTF8.GetString(html.WrittenSpan);
        await Assert.That(output).Contains("<h1>");
        await Assert.That(RecordingPlugin.LastPath).IsEqualTo("intro.md");
    }
}
