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
        AutorefsRegistry registry = new();
        registry.Register("Foo.Bar"u8, [.. "api/Foo.Bar.html"u8], fragment: default);
        registry.Register("Foo.Baz"u8, [.. "api/Foo.Baz.html"u8], fragment: default);
        XrefsPlugin plugin = new(registry, XrefsOptions.Default);

        await plugin.FinalizeAsync(new(temp.Root, []), CancellationToken.None);

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
        XrefMapWriter.Write(importPath, baseUrl: [], [([.. "System.String"u8], [.. "api/System.String.html"u8])]);

        AutorefsRegistry registry = new();
        var options = XrefsOptions.Default with
        {
            Imports = [new(importPath, "https://docs.microsoft.com/dotnet/")]
        };
        XrefsPlugin plugin = new(registry, options);

        await plugin.ConfigureAsync(new("/in", temp.Root, [], new()), CancellationToken.None);

        var resolved = registry.TryResolve("System.String"u8, out var url);
        await Assert.That(resolved).IsTrue();
        await Assert.That(url.AsSpan().SequenceEqual("https://docs.microsoft.com/dotnet/api/System.String.html"u8)).IsTrue();
    }

    /// <summary>An imported file's <c>baseUrl</c> is honored when no override is supplied.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmbeddedBaseUrlIsHonored()
    {
        using var temp = TempDir.Create();
        var importPath = Path.Combine(temp.Root, "external.json");
        XrefMapWriter.Write(importPath, baseUrl: [.. "https://example.com/docs/"u8], [([.. "Foo"u8], [.. "api/Foo.html"u8])]);

        AutorefsRegistry registry = new();
        var options = XrefsOptions.Default with
        {
            Imports = [new(importPath)]
        };
        XrefsPlugin plugin = new(registry, options);

        await plugin.ConfigureAsync(new("/in", temp.Root, [], new()), CancellationToken.None);

        registry.TryResolve("Foo"u8, out var url);
        await Assert.That(url.AsSpan().SequenceEqual("https://example.com/docs/api/Foo.html"u8)).IsTrue();
    }

    /// <summary>EmitMap=false skips the xrefmap.json file write at finalize time.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmitMapFalseSkipsFileWrite()
    {
        using var temp = TempDir.Create();
        AutorefsRegistry registry = new();
        registry.Register("Foo"u8, [.. "f.html"u8], fragment: default);
        var options = XrefsOptions.Default with { EmitMap = false };
        XrefsPlugin plugin = new(registry, options);

        await plugin.FinalizeAsync(new(temp.Root, []), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(temp.Root, "xrefmap.json"))).IsFalse();
    }

    /// <summary>Missing import sources are silently skipped (offline / fetch failure).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingImportIsSkipped()
    {
        using var temp = TempDir.Create();
        AutorefsRegistry registry = new();
        var options = XrefsOptions.Default with
        {
            Imports = [new(Path.Combine(temp.Root, "does-not-exist.json"))]
        };
        XrefsPlugin plugin = new(registry, options);

        await plugin.ConfigureAsync(new("/in", temp.Root, [], new()), CancellationToken.None);

        await Assert.That(registry.Count).IsEqualTo(0);
    }
}
