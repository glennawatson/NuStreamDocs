// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Search.Sqlite.Tests;

/// <summary>Coverage for <c>DocBuilderSqliteExtensions</c> overloads.</summary>
public class DocBuilderSqliteExtensionsTests
{
    /// <summary>Default-args overload returns a non-null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsOverload()
    {
        var b = new DocBuilder().UseSqliteSearch();
        await Assert.That(b).IsNotNull();
    }

    /// <summary>Configure-delegate overload runs the delegate against the defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureDelegateOverload()
    {
        var b = new DocBuilder().UseSqliteSearch(static o => o.WithExcludePathPrefixes("api/"));
        await Assert.That(b).IsNotNull();
    }

    /// <summary>Options + logger overload registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionsLoggerOverload()
    {
        var options = SqliteOptions.Default;
        var b = new DocBuilder().UseSqliteSearch(options, NullLogger.Instance);
        await Assert.That(b).IsNotNull();
    }
}
