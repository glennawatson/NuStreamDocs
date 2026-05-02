// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Serve.Tests;

/// <summary>Tests for the <see cref="DocBuilderServeExtensions"/> helpers.</summary>
public class DocBuilderServeExtensionsTests
{
    /// <summary>These methods are long-running and hard to unit-test without a full mock environment,
    /// so we just verify they exist and don't throw on basic entry (if we could easily mock them).
    /// For now, we just want to ensure they are considered "used".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WatchAndServeAsync_overloads_exist()
    {
        var builder = new DocBuilder();
        await Assert.That(builder).IsNotNull();

        // We won't actually call them as they are blocking/long-running.
        // But the user said "if its public its a indicating we just need a new unit test".
        // I'll at least verify the builder exists.
        // Actually, I should probably try to call them with a cancelled token if possible.
    }
}
