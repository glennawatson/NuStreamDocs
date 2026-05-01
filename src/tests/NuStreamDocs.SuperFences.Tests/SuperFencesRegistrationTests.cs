// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.SuperFences.Tests;

/// <summary>Tests for <c>SuperFencesPlugin</c> registration and name.</summary>
public class SuperFencesRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new SuperFencesPlugin().Name).IsEqualTo("superfences");

    /// <summary>UseSuperFences() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSuperFencesRegisters() =>
        await Assert.That(new DocBuilder().UseSuperFences()).IsTypeOf<DocBuilder>();

    /// <summary>UseSuperFences rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSuperFencesRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderSuperFencesExtensions.UseSuperFences(null!));
        await Assert.That(ex).IsNotNull();
    }
}
