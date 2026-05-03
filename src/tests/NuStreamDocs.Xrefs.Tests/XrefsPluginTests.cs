// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Autorefs;

namespace NuStreamDocs.Xrefs.Tests;

/// <summary>End-to-end tests for <c>XrefsPlugin</c>.</summary>
public class XrefsPluginTests
{
    /// <summary>The plugin emits an xrefmap covering every entry in the shared registry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FinalizeEmitsRegistrySnapshot()
    {
        using var temp = TempDir.Create();
        var registry = new AutorefsRegistry();
        registry.Register("Foo.Bar", "api/Foo.Bar.html", fragment: null);
        registry.Register("Foo.Baz", "api/Foo.Baz.html", fragment: null);
        var plugin = new XrefsPlugin(registry, XrefsOptions.Default);

        await plugin.OnFinalizeAsync(new(temp.Root), CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(Path.Combine(temp.Root, "xrefmap.json"));
        var payload = XrefMapReader.Read(bytes);
        await Assert.That(payload.Entries.Length).IsEqualTo(2);
    }

    /// <summary>Imports register their entries into the shared registry with the configured base URL prepended.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ImportsRegisterUnderBaseUrl()
    {
        using var temp = TempDir.Create();
        var importPath = Path.Combine(temp.Root, "external.json");
        XrefMapWriter.Write(importPath, baseUrl: string.Empty, [("System.String", "api/System.String.html")]);

        var registry = new AutorefsRegistry();
        var options = XrefsOptions.Default with
        {
            Imports = [new(importPath, "https://docs.microsoft.com/dotnet/")],
        };
        var plugin = new XrefsPlugin(registry, options);

        await plugin.OnConfigureAsync(new("/in", temp.Root, []), CancellationToken.None);

        var resolved = registry.TryResolve("System.String", out var url);
        await Assert.That(resolved).IsTrue();
        await Assert.That(url).IsEqualTo("https://docs.microsoft.com/dotnet/api/System.String.html");
    }

    /// <summary>An imported file's <c>baseUrl</c> is honored when no override is supplied.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmbeddedBaseUrlIsHonored()
    {
        using var temp = TempDir.Create();
        var importPath = Path.Combine(temp.Root, "external.json");
        XrefMapWriter.Write(importPath, baseUrl: "https://example.com/docs/", [("Foo", "api/Foo.html")]);

        var registry = new AutorefsRegistry();
        var options = XrefsOptions.Default with
        {
            Imports = [new(importPath)],
        };
        var plugin = new XrefsPlugin(registry, options);

        await plugin.OnConfigureAsync(new("/in", temp.Root, []), CancellationToken.None);

        registry.TryResolve("Foo", out var url);
        await Assert.That(url).IsEqualTo("https://example.com/docs/api/Foo.html");
    }

    /// <summary>EmitMap=false skips the xrefmap.json file write at finalize time.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmitMapFalseSkipsFileWrite()
    {
        using var temp = TempDir.Create();
        var registry = new AutorefsRegistry();
        registry.Register("Foo", "f.html", fragment: null);
        var options = XrefsOptions.Default with { EmitMap = false };
        var plugin = new XrefsPlugin(registry, options);

        await plugin.OnFinalizeAsync(new(temp.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(temp.Root, "xrefmap.json"))).IsFalse();
    }

    /// <summary>Missing import sources are silently skipped (offline / fetch failure).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingImportIsSkipped()
    {
        using var temp = TempDir.Create();
        var registry = new AutorefsRegistry();
        var options = XrefsOptions.Default with
        {
            Imports = [new(Path.Combine(temp.Root, "does-not-exist.json"))],
        };
        var plugin = new XrefsPlugin(registry, options);

        await plugin.OnConfigureAsync(new("/in", temp.Root, []), CancellationToken.None);

        await Assert.That(registry.Count).IsEqualTo(0);
    }
}
