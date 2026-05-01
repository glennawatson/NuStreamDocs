// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Icons.FontAwesome.Tests;

/// <summary>End-to-end tests for the Font Awesome icon plugin.</summary>
public class FontAwesomePluginTests
{
    /// <summary>Default options should emit a Font Awesome stylesheet link.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DefaultOptionsEmitFaStylesheet()
    {
        var html = WriteHeadExtras(new FontAwesomePlugin());
        await Assert.That(html).Contains("fontawesome");
        await Assert.That(html).Contains("crossorigin=\"anonymous\"");
        await Assert.That(html).Contains("referrerpolicy=\"no-referrer\"");
        await Assert.That(html).StartsWith("<link rel=\"stylesheet\" href=\"");
    }

    /// <summary>A custom URL flows through to the rendered link tag.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CustomUrlFlowsThrough()
    {
        var plugin = new FontAwesomePlugin(FontAwesomeOptions.Default with
        {
            StylesheetUrl = "https://example.test/fa.css",
        });
        var html = WriteHeadExtras(plugin);
        await Assert.That(html).Contains("https://example.test/fa.css");
    }

    /// <summary>Empty stylesheet URL should produce no output.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyUrlProducesNothing()
    {
        var plugin = new FontAwesomePlugin(FontAwesomeOptions.Default with { StylesheetUrl = string.Empty });
        var html = WriteHeadExtras(plugin);
        await Assert.That(html).IsEqualTo(string.Empty);
    }

    /// <summary>Helper: invoke <c>IHeadExtraProvider.WriteHeadExtra</c> and decode the bytes.</summary>
    /// <param name="provider">Provider under test.</param>
    /// <returns>The rendered head-extras HTML string.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "Test deliberately exercises the IHeadExtraProvider contract.")]
    private static string WriteHeadExtras(IHeadExtraProvider provider)
    {
        var writer = new ArrayBufferWriter<byte>();
        provider.WriteHeadExtra(writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
