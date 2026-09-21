// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Tests for <c>DocBuilder</c> plugin registration + render.</summary>
public class DocBuilderTests
{
    /// <summary>Source-relative path of the page rendered by the tests.</summary>
    private const string PagePath = "intro.md";

    /// <summary>UsePlugin&lt;T&gt; should construct via the parameterless ctor and fire the page hook.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UsePluginGenericFiresRenderHook()
    {
        var builder = new DocBuilder().UsePlugin<RecordingPlugin>();
        ArrayBufferWriter<byte> html = new();
        await builder.RenderPageAsync(PagePath, new([.. "# Hi"u8]), html, CancellationToken.None);

        var output = Encoding.UTF8.GetString(html.WrittenSpan);
        await Assert.That(output).Contains("<h1>");
        await Assert.That(RecordingPlugin.LastPath).IsEqualTo(PagePath);
    }

    /// <summary>Rendering a page through one rewriting plugin returns each pooled buffer to the pool exactly once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RenderPageReturnsEachPooledBufferOnce()
    {
        const int RentCount = 4;
        var builder = new DocBuilder().UsePlugin<RecordingPlugin>();
        ArrayBufferWriter<byte> html = new();
        await builder.RenderPageAsync(PagePath, new([.. "# Hi"u8]), html, CancellationToken.None);

        var rentals = new PageBuilderRental[RentCount];
        try
        {
            HashSet<ArrayBufferWriter<byte>> writers = [with(ReferenceEqualityComparer.Instance)];
            for (var i = 0; i < rentals.Length; i++)
            {
                rentals[i] = PageBuilderPool.Rent();
                _ = writers.Add(rentals[i].Writer);
            }

            await Assert.That(writers.Count).IsEqualTo(RentCount);
        }
        finally
        {
            for (var i = 0; i < rentals.Length; i++)
            {
                rentals[i].Dispose();
            }
        }
    }
}
