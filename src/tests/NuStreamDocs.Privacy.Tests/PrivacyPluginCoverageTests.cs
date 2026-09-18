// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Coverage for PrivacyPlugin.Name and AuditedUrls.</summary>
public class PrivacyPluginCoverageTests
{
    /// <summary>Name returns "privacy".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        PrivacyPlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("privacy"u8)).IsTrue();
    }

    /// <summary>AuditedUrls is empty before any pages are scanned.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AuditedUrlsEmpty()
    {
        PrivacyPlugin plugin = new();
        await Assert.That(plugin.AuditedUrls.Length).IsEqualTo(0);
    }

    /// <summary>Audit manifests support paths on either side of the stack-buffer limit.</summary>
    /// <param name="length">Relative manifest path length in characters.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(255)]
    [Arguments(256)]
    [Arguments(257)]
    public async Task FinalizeWritesLongAuditManifestPath(int length)
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var segment = new string('a', length - "nested//audit.json".Length);
            var relativePath = $"nested/{segment}/audit.json";
            var options = PrivacyOptions.Default with
            {
                AuditOnly = true,
                AuditManifestPath = Encoding.UTF8.GetBytes(relativePath),
            };
            var plugin = new PrivacyPlugin(options);
            await plugin.FinalizeAsync(new(directory.FullName, []), CancellationToken.None);

            var contents = await File.ReadAllTextAsync(Path.Combine(directory.FullName, relativePath));
            using var manifest = JsonDocument.Parse(contents);
            await Assert.That(manifest.RootElement.GetProperty("auditOnly").GetBoolean()).IsTrue();
            await Assert.That(manifest.RootElement.GetProperty("urls").GetArrayLength()).IsEqualTo(0);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
