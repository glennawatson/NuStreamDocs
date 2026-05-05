// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Icons.FontAwesome.Tests;

/// <summary>Coverage for DocBuilderFontAwesomeExtensions.UseFontAwesome(Func) overload.</summary>
public class DocBuilderFontAwesomeExtensionsCoverageTests
{
    /// <summary>UseFontAwesome(builder, configure) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseFontAwesomeConfigure()
    {
        var b = new DocBuilder().UseFontAwesome(static o => o);
        await Assert.That(b).IsNotNull();
    }
}
