// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc.Tests;

/// <summary>Lifecycle method coverage for TocPlugin.</summary>
public class TocPluginLifecycleTests
{
    /// <summary>Name and TocMarker accessors return their constants.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAndMarker()
    {
        TocPlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("toc"u8)).IsTrue();
        await Assert.That(System.Text.Encoding.UTF8.GetString(TocPlugin.TocMarker)).IsEqualTo("<!--@@toc@@-->");
    }
}
