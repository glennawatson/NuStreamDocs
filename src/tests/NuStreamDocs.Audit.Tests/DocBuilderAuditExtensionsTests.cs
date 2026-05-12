// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Audit.Tests;

/// <summary>Coverage for the <c>DocBuilderAuditExtensions</c> overloads.</summary>
public class DocBuilderAuditExtensionsTests
{
    /// <summary>The default-args overload returns a non-null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsOverload()
    {
        var b = new DocBuilder().UseAudit();
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The configure-delegate overload runs the delegate against the defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureDelegateOverload()
    {
        var b = new DocBuilder().UseAudit(static o => o.WithStrict().Disable(AuditRule.PositiveTabIndex));
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The options overload registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionsOverload()
    {
        var b = new DocBuilder().UseAudit(AuditOptions.Default.WithStrict());
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The options-and-logger overload registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionsAndLoggerOverload()
    {
        var b = new DocBuilder().UseAudit(AuditOptions.Default, NullLogger.Instance);
        await Assert.That(b).IsNotNull();
    }
}
