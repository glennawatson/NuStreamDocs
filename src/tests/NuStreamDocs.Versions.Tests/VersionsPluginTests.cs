// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Versions.Tests;

/// <summary>End-to-end tests for <c>VersionsPlugin</c>.</summary>
public class VersionsPluginTests
{
    /// <summary>OnFinalize writes the manifest into the parent of the output root.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnFinalizeWritesManifest()
    {
        var siteRoot = Path.Combine(Path.GetTempPath(), "smd-vplugin-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var versionRoot = Path.Combine(siteRoot, "0.4.2");
        Directory.CreateDirectory(versionRoot);

        try
        {
            VersionsPlugin plugin = new(VersionOptions.Latest("0.4.2", "0.4 (latest)"));
            BuildFinalizeContext context = new(versionRoot, []);
            await plugin.FinalizeAsync(context, CancellationToken.None);

            var entries = VersionsManifest.Read(siteRoot);
            await Assert.That(entries.Count).IsEqualTo(1);
            await Assert.That(entries[0].Version).IsEqualTo("0.4.2");
            await Assert.That(Encoding.UTF8.GetString(entries[0].Aliases[0])).IsEqualTo("latest");
        }
        finally
        {
            Directory.Delete(siteRoot, recursive: true);
        }
    }

    /// <summary>A second build for the same version updates the existing entry rather than appending.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SecondBuildUpdatesEntry()
    {
        var siteRoot = Path.Combine(Path.GetTempPath(), "smd-vplugin-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var versionRoot = Path.Combine(siteRoot, "0.4.2");
        Directory.CreateDirectory(versionRoot);

        try
        {
            VersionsPlugin first = new(new("0.4.2", "0.4 (initial)"));
            VersionsPlugin second = new(new("0.4.2", "0.4 (refreshed)", [[.. "latest"u8]]));
            BuildFinalizeContext context = new(versionRoot, []);

            await first.FinalizeAsync(context, CancellationToken.None);
            await second.FinalizeAsync(context, CancellationToken.None);

            var entries = VersionsManifest.Read(siteRoot);
            await Assert.That(entries.Count).IsEqualTo(1);
            await Assert.That(entries[0].Title).IsEqualTo("0.4 (refreshed)");
            await Assert.That(entries[0].Aliases.Count).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(siteRoot, recursive: true);
        }
    }
}
