// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Branch-coverage tests for LocalAssemblySource.BuildFallbackIndex.</summary>
public class LocalAssemblySourceTests
{
    /// <summary>The fallback index includes dlls next to every supplied assembly path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndexCoversAssemblyDirectories()
    {
        using TempDir dir = new();
        var asmA = await Touch(dir.Root, "A.dll");
        await Touch(dir.Root, "Sibling.dll");
        LocalAssemblySource src = new("net10.0", [asmA], []);
        var group = await FirstGroup(src);
        await Assert.That(group.FallbackIndex).ContainsKey("A.dll");
        await Assert.That(group.FallbackIndex).ContainsKey("Sibling.dll");
    }

    /// <summary>Empty directory components are tolerated (no crash).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyDirectoryComponentSkipped()
    {
        LocalAssemblySource src = new("net10.0", ["bare.dll"], []);
        var group = await FirstGroup(src);
        await Assert.That(group.FallbackIndex.Count).IsEqualTo(0);
    }

    /// <summary>Fallback search paths add their dlls to the index.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FallbackSearchPathsAdded()
    {
        using TempDir asmDir = new();
        using TempDir fallbackDir = new();
        var asmA = await Touch(asmDir.Root, "A.dll");
        await Touch(fallbackDir.Root, "Fallback.dll");
        LocalAssemblySource src = new("net10.0", [asmA], [fallbackDir.Root]);
        var group = await FirstGroup(src);
        await Assert.That(group.FallbackIndex).ContainsKey("Fallback.dll");
    }

    /// <summary>A non-existent fallback search path is silently skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonexistentSearchPathSkipped()
    {
        using TempDir asmDir = new();
        var asmA = await Touch(asmDir.Root, "A.dll");
        LocalAssemblySource src = new("net10.0", [asmA], ["/does-not-exist-" + Guid.NewGuid().ToString("N")]);
        var group = await FirstGroup(src);
        await Assert.That(group.FallbackIndex).ContainsKey("A.dll");
    }

    /// <summary>The first directory wins on duplicate filenames.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DuplicateFilenameFirstWins()
    {
        using TempDir asmDir = new();
        using TempDir fallbackDir = new();
        var winner = await Touch(asmDir.Root, "Dup.dll");
        await Touch(fallbackDir.Root, "Dup.dll");
        await Touch(asmDir.Root, "Asm.dll");
        LocalAssemblySource src = new("net10.0", [Path.Combine(asmDir.Root, "Asm.dll")], [fallbackDir.Root]);
        var group = await FirstGroup(src);
        await Assert.That(group.FallbackIndex["Dup.dll"]).IsEqualTo(winner);
    }

    /// <summary>Creates an empty file at <paramref name="dir"/>/<paramref name="name"/>.</summary>
    /// <param name="dir">Containing directory.</param>
    /// <param name="name">File name.</param>
    /// <returns>Absolute path of the created file.</returns>
    private static async Task<string> Touch(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        await File.WriteAllTextAsync(path, string.Empty);
        return path;
    }

    /// <summary>Returns the first AssemblyGroup yielded by <paramref name="src"/>.</summary>
    /// <param name="src">Source to enumerate.</param>
    /// <returns>The first yielded group.</returns>
    private static async Task<AssemblyGroup> FirstGroup(LocalAssemblySource src)
    {
        List<AssemblyGroup> groups = [];
        await foreach (var g in src.DiscoverAsync())
        {
            groups.Add(g);
        }

        return groups[0];
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-las-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch root.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
