// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Search.Tests;

/// <summary>Coverage for DocBuilderSearchExtensions.UseSearch(opts, logger) overload.</summary>
public class DocBuilderSearchExtensionsCoverageTests
{
    /// <summary>UseSearch(builder, options, logger) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSearchOptionsLogger()
    {
        SearchOptions options = default;
        var b = new DocBuilder().UseSearch(options, NullLogger.Instance);
        await Assert.That(b).IsNotNull();
    }
}
