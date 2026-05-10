// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Toc.Tests;

/// <summary>Builder-extension tests for <c>TocPlugin</c>.</summary>
public class TocRegistrationTests
{
    /// <summary>UseToc() registers the plugin with defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseTocRegisters() =>
        await Assert.That(new DocBuilder().UseToc()).IsTypeOf<DocBuilder>();

    /// <summary>UseToc(options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseTocOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseToc(TocOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseToc(options, logger) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseTocOptionsLoggerRegisters() =>
        await Assert.That(new DocBuilder().UseToc(TocOptions.Default, NullLogger.Instance)).IsTypeOf<DocBuilder>();
}
