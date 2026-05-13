// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Audit.Tests;

/// <summary>Coverage for <see cref="AuditPlugin"/> against an on-disk output tree.</summary>
public class AuditPluginTests
{
    /// <summary>The plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new AuditPlugin().Name.SequenceEqual("audit"u8)).IsTrue();

    /// <summary>Running over a tree with a flawed page surfaces findings ordered by page then rule.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunAsyncCollectsAndOrdersFindings()
    {
        var dir = Directory.CreateTempSubdirectory("nstd-audit-");
        try
        {
            const string GoodHtml = "<html lang=\"en\"><head><title>t</title>"
                                    + "<meta name=\"viewport\" content=\"x\"></head><body><h1>h</h1></body></html>";
            const string BadHtml = "<html><head><title>t</title></head>"
                                   + "<body><h1>a</h1><h3>b</h3><img src=\"x.png\"></body></html>";
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "good.html"), GoodHtml);
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "bad.html"), BadHtml);

            var plugin = new AuditPlugin();
            var diagnostics = await plugin.RunAsync(new(dir.FullName), CancellationToken.None);

            await Assert.That(diagnostics.Length).IsGreaterThan(0);
            await Assert
                .That(Array.TrueForAll(diagnostics, d => string.Equals(d.Page, "bad.html", StringComparison.Ordinal)))
                .IsTrue();
            await Assert.That(diagnostics.Select(d => d.Rule)).Contains(AuditRule.ImageMissingAlt);
            await Assert.That(diagnostics.Select(d => d.Rule)).Contains(AuditRule.HeadingLevelSkipped);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>A missing output directory yields no findings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingOutputRootIsEmpty()
    {
        var plugin = new AuditPlugin();
        var diagnostics =
            await plugin.RunAsync(
                new(Path.Combine(Path.GetTempPath(), "nstd-audit-does-not-exist-" + Guid.NewGuid().ToString("N"))),
                CancellationToken.None);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>An empty output root passes <see cref="ArgumentException"/> through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyOutputRootThrows()
    {
        var plugin = new AuditPlugin();
        await Assert.That(async () => _ = await plugin.RunAsync(default, CancellationToken.None))
            .Throws<ArgumentException>();
    }

    /// <summary>A null options argument is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullOptionsThrows() =>
        await Assert.That(static () => new AuditPlugin(null!)).Throws<ArgumentNullException>();
}
