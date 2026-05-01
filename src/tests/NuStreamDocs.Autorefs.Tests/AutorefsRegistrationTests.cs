// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Builder-extension tests for <c>AutorefsPlugin</c>.</summary>
public class AutorefsRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new AutorefsPlugin().Name).IsEqualTo("autorefs");

    /// <summary>UseAutorefs() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseAutorefsRegisters() =>
        await Assert.That(new DocBuilder().UseAutorefs()).IsTypeOf<DocBuilder>();

    /// <summary>UseAutorefs(registry) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseAutorefsRegistryRegisters() =>
        await Assert.That(new DocBuilder().UseAutorefs(new AutorefsRegistry())).IsTypeOf<DocBuilder>();

    /// <summary>UseAutorefs(registry, logger) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseAutorefsRegistryLoggerRegisters() =>
        await Assert.That(new DocBuilder().UseAutorefs(new AutorefsRegistry(), NullLogger.Instance)).IsTypeOf<DocBuilder>();

    /// <summary>UseAutorefs rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseAutorefsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderAutorefsExtensions.UseAutorefs(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseAutorefs(registry) rejects null registry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseAutorefsRejectsNullRegistry()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseAutorefs((AutorefsRegistry)null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseAutorefs(registry, logger) rejects null logger.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseAutorefsRejectsNullLogger()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseAutorefs(new AutorefsRegistry(), null!));
        await Assert.That(ex).IsNotNull();
    }
}
