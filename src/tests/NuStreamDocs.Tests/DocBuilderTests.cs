// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>Tests for <c>DocBuilder</c> plugin registration + render.</summary>
public class DocBuilderTests
{
    /// <summary>Source-relative path of the page rendered by the tests.</summary>
    private const string PagePath = "intro.md";

    /// <summary>Marker appended by the first marker plugin.</summary>
    private const string FirstMarker = "<!--a-->";

    /// <summary>Marker appended by the second marker plugin.</summary>
    private const string SecondMarker = "<!--b-->";

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
        var builder = new DocBuilder().UsePlugin<RecordingPlugin>();
        ArrayBufferWriter<byte> html = new();
        await builder.RenderPageAsync(PagePath, new([.. "# Hi"u8]), html, CancellationToken.None);

        await AssertRentsDistinctWriters();
    }

    /// <summary>Rendering a page through two rewriting plugins, which leaves the final HTML in the input buffer, returns each pooled buffer to the pool exactly once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RenderPageReturnsEachPooledBufferOnceAfterTwoRewrites()
    {
        var builder = new DocBuilder()
            .UsePlugin(MarkerPlugin(FirstMarker, 0))
            .UsePlugin(MarkerPlugin(SecondMarker, 1));
        ArrayBufferWriter<byte> html = new();
        await builder.RenderPageAsync(PagePath, new([.. "# Hi"u8]), html, CancellationToken.None);

        await Assert.That(Encoding.UTF8.GetString(html.WrittenSpan)).EndsWith(FirstMarker + SecondMarker);
        await AssertRentsDistinctWriters();
    }

    /// <summary>A plugin registered after an earlier render takes part in later renders.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RenderPageAppliesPluginRegisteredAfterEarlierRender()
    {
        var builder = new DocBuilder().UsePlugin(MarkerPlugin(FirstMarker, 0));
        ArrayBufferWriter<byte> first = new();
        await builder.RenderPageAsync(PagePath, new([.. "# Hi"u8]), first, CancellationToken.None);

        _ = builder.UsePlugin(MarkerPlugin(SecondMarker, 1));
        ArrayBufferWriter<byte> second = new();
        await builder.RenderPageAsync(PagePath, new([.. "# Hi"u8]), second, CancellationToken.None);

        await Assert.That(Encoding.UTF8.GetString(first.WrittenSpan)).EndsWith(FirstMarker);
        await Assert.That(Encoding.UTF8.GetString(first.WrittenSpan)).DoesNotContain(SecondMarker);
        await Assert.That(Encoding.UTF8.GetString(second.WrittenSpan)).EndsWith(FirstMarker + SecondMarker);
    }

    /// <summary>Creates a post-render plugin that appends <paramref name="marker"/> in the normal band.</summary>
    /// <param name="marker">Text appended after the rendered HTML.</param>
    /// <param name="tiebreak">Ordering tiebreak within the band.</param>
    /// <returns>The plugin.</returns>
    private static MarkerAppendPlugin MarkerPlugin(string marker, int tiebreak) =>
        new(Encoding.UTF8.GetBytes(marker), new(PluginBand.Normal, tiebreak));

    /// <summary>Rents several buffers and asserts the pool hands out distinct writers, which fails when any writer was parked twice.</summary>
    /// <returns>A task representing the asynchronous assertion.</returns>
    private static async Task AssertRentsDistinctWriters()
    {
        const int RentCount = 4;
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
