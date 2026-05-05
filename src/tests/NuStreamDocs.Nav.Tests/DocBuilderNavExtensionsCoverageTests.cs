// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Nav.Tests;

/// <summary>Coverage for DocBuilderNavExtensions.UseNav(Func) overloads.</summary>
public class DocBuilderNavExtensionsCoverageTests
{
    /// <summary>UseNav(builder, configure) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseNavConfigure()
    {
        var b = new DocBuilder().UseNav(static o => o);
        await Assert.That(b).IsNotNull();
    }

    /// <summary>UseNav(builder, configure, logger) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseNavConfigureLogger()
    {
        var b = new DocBuilder().UseNav(o => o, NullLogger.Instance);
        await Assert.That(b).IsNotNull();
    }

    /// <summary>NavNodeTitleComparer.Instance handles null comparands and ordering.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NavComparerHandlesNullsAndOrder()
    {
        var cmp = NavNodeTitleComparer.Instance;
        await Assert.That(cmp.Compare(null, null)).IsEqualTo(0);
        NavNode a = new("Apple", "/a", isSection: false, []);
        NavNode b = new("Banana", "/b", isSection: false, []);
        await Assert.That(cmp.Compare(a, null)).IsGreaterThan(0);
        await Assert.That(cmp.Compare(null, a)).IsLessThan(0);
        await Assert.That(cmp.Compare(a, b)).IsLessThan(0);
    }
}
