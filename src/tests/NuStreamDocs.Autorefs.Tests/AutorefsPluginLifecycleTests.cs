// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Lifecycle method coverage for <c>AutorefsPlugin</c>.</summary>
public class AutorefsPluginLifecycleTests
{
    /// <summary>ConfigureAsync registers the autoref cross-page marker and clears prior state.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureAsync()
    {
        AutorefsPlugin plugin = new();
        CrossPageMarkerRegistry markers = new();
        await plugin.ConfigureAsync(new("/in", "/out", [], markers), CancellationToken.None);
        await Assert.That(markers.Markers.Count).IsGreaterThan(0);
    }

    /// <summary>Scan publishes heading IDs into the registry from the post-render HTML.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScanRegistersHeadings()
    {
        AutorefsPlugin plugin = new();
        ScanHtml(plugin, "<h1 id=\"hello\">Hello</h1>"u8);
        await Assert.That(plugin.Registry.Count).IsGreaterThanOrEqualTo(1);
    }

    /// <summary>FinalizeAsync runs even when the registry is empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FinalizeAsyncEmpty()
    {
        using TempDir temp = new();
        await new AutorefsPlugin().FinalizeAsync(new(temp.Root, []), CancellationToken.None);
    }

    /// <summary>Drives one Scan call against the plugin.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Rendered HTML bytes.</param>
    private static void ScanHtml(AutorefsPlugin plugin, ReadOnlySpan<byte> html)
    {
        PageScanContext ctx = new("p.md", default, html);
        plugin.Scan(in ctx);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-ar-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch directory.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
