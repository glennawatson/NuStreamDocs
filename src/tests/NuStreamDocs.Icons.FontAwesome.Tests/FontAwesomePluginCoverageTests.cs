// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Icons.FontAwesome.Tests;

/// <summary>Coverage for FontAwesomePlugin.Name.</summary>
public class FontAwesomePluginCoverageTests
{
    /// <summary>Name returns "fontawesome".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        var plugin = new FontAwesomePlugin();
        await Assert.That(plugin.Name.SequenceEqual("fontawesome"u8)).IsTrue();
    }
}
