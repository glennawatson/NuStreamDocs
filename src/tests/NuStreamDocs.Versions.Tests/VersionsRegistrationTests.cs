// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Versions.Tests;

/// <summary>Builder-extension tests for <c>VersionsPlugin</c>.</summary>
public class VersionsRegistrationTests
{
    /// <summary>UseVersions(options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseVersionsRegisters()
    {
        var options = new VersionOptions("1.0", "Stable");
        await Assert.That(new DocBuilder().UseVersions(options)).IsTypeOf<DocBuilder>();
    }

    /// <summary>UseVersions(options, logger) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseVersionsLoggerRegisters()
    {
        var options = new VersionOptions("1.0", "Stable");
        await Assert.That(new DocBuilder().UseVersions(options, NullLogger.Instance)).IsTypeOf<DocBuilder>();
    }

    /// <summary>Latest(...) populates the <c>latest</c> alias.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LatestPopulatesAlias()
    {
        var opts = VersionOptions.Latest("1.2", "Recent");
        await Assert.That(opts.Aliases.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(opts.Aliases[0])).IsEqualTo("latest");
    }

    /// <summary>Validate() throws on empty version.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnEmptyVersion()
    {
        var opts = new VersionOptions(string.Empty, "Title");
        var ex = Assert.Throws<ArgumentException>(opts.Validate);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Validate() throws on empty title.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnEmptyTitle()
    {
        var opts = new VersionOptions("1.0", string.Empty);
        var ex = Assert.Throws<ArgumentException>(opts.Validate);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>ToEntry() copies version/title/aliases.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToEntryCopies()
    {
        var entry = new VersionOptions("1.0", "Stable", [[.. "latest"u8], [.. "v1"u8]]).ToEntry();
        await Assert.That(entry.Version).IsEqualTo("1.0");
        await Assert.That(entry.Title).IsEqualTo("Stable");
        await Assert.That(entry.Aliases.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(entry.Aliases[0])).IsEqualTo("latest");
        await Assert.That(Encoding.UTF8.GetString(entry.Aliases[1])).IsEqualTo("v1");
    }

    /// <summary>UseVersions rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseVersionsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderVersionsExtensions.UseVersions(null!, new("1.0", "X")));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseVersions rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseVersionsRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseVersions(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseVersions(options, logger) rejects null logger.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseVersionsLoggerRejectsNullLogger()
    {
        var options = new VersionOptions("1.0", "Stable");
        var ex = Assert.Throws<ArgumentNullException>(() => new DocBuilder().UseVersions(options, null!));
        await Assert.That(ex).IsNotNull();
    }
}
