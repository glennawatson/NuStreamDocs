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
    /// <summary>Display title for the stable release.</summary>
    private const string StableTitle = "Stable";

    /// <summary>Alias identifying the latest release.</summary>
    private const string LatestAlias = "latest";

    /// <summary>Expected aliases copied from the version options.</summary>
    private const int CopiedAliasCount = 2;

    /// <summary>UseVersions(options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseVersionsRegisters()
    {
        VersionOptions options = new("1.0", StableTitle);
        await Assert.That(new DocBuilder().UseVersions(options)).IsTypeOf<DocBuilder>();
    }

    /// <summary>UseVersions(options, logger) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseVersionsLoggerRegisters()
    {
        VersionOptions options = new("1.0", StableTitle);
        await Assert.That(new DocBuilder().UseVersions(options, NullLogger.Instance)).IsTypeOf<DocBuilder>();
    }

    /// <summary>Latest(...) populates the <c>latest</c> alias.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LatestPopulatesAlias()
    {
        var opts = VersionOptions.Latest("1.2", "Recent");
        await Assert.That(opts.Aliases.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(opts.Aliases[0])).IsEqualTo(LatestAlias);
    }

    /// <summary>Validate() throws on empty version.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnEmptyVersion()
    {
        VersionOptions opts = new(string.Empty, "Title");
        var ex = Assert.Throws<ArgumentException>(opts.Validate);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Validate() throws on empty title.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnEmptyTitle()
    {
        VersionOptions opts = new("1.0", string.Empty);
        var ex = Assert.Throws<ArgumentException>(opts.Validate);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>ToEntry() copies version/title/aliases.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToEntryCopies()
    {
        var entry = new VersionOptions("1.0", StableTitle, [[.. "latest"u8], [.. "v1"u8]]).ToEntry();
        await Assert.That(entry.Version).IsEqualTo("1.0");
        await Assert.That(entry.Title).IsEqualTo(StableTitle);
        await Assert.That(entry.Aliases.Length).IsEqualTo(CopiedAliasCount);
        await Assert.That(Encoding.UTF8.GetString(entry.Aliases[0])).IsEqualTo(LatestAlias);
        await Assert.That(Encoding.UTF8.GetString(entry.Aliases[1])).IsEqualTo("v1");
    }
}
