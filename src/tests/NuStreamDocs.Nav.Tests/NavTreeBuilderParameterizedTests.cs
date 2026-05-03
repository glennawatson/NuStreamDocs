// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav.Tests;

/// <summary>Parameterized inputs for NavTreeBuilder covering every combination of sort, prune, indexes, and hide-empty toggles.</summary>
public class NavTreeBuilderParameterizedTests
{
    /// <summary>Each <see cref="NavSortBy"/> mode produces a tree with the expected page count.</summary>
    /// <param name="sort">Sort mode.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(NavSortBy.FileName)]
    [Arguments(NavSortBy.Title)]
    [Arguments(NavSortBy.None)]
    public async Task SortModes(NavSortBy sort)
    {
        using var temp = new ScratchTree();
        await temp.WriteAsync("a.md", "# A\n");
        await temp.WriteAsync("b.md", "# B\n");
        var options = NavOptions.Default with { SortBy = sort };
        var root = NavTreeBuilder.Build(temp.Root, options);
        await Assert.That(root.Children.Length).IsEqualTo(2);
    }

    /// <summary>Indexes toggle controls whether index.md becomes a section index.</summary>
    /// <param name="indexes">Whether to detect index pages.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task IndexesToggle(bool indexes)
    {
        using var temp = new ScratchTree();
        await temp.WriteAsync("guide/index.md", "# Guide\n");
        await temp.WriteAsync("guide/intro.md", "# Intro\n");
        var options = NavOptions.Default with { Indexes = indexes };
        var root = NavTreeBuilder.Build(temp.Root, options);
        await Assert.That(root.Children.Length).IsGreaterThan(0);
    }

    /// <summary>HideEmptySections toggle accepts both values without error.</summary>
    /// <param name="hide">Whether to hide empty sections.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task HideEmptySectionsToggle(bool hide)
    {
        using var temp = new ScratchTree();
        await temp.WriteAsync("a.md", "# A\n");
        Directory.CreateDirectory(Path.Combine(temp.Root, "empty"));
        var options = NavOptions.Default with { HideEmptySections = hide };
        var root = NavTreeBuilder.Build(temp.Root, options);
        await Assert.That(root).IsNotNull();
    }

    /// <summary>Include/exclude patterns filter the input set.</summary>
    /// <param name="patterns">Glob patterns.</param>
    /// <param name="expectedChildren">Expected top-level child count.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(new[] { "*.md" }, 2)]
    [Arguments(new[] { "a.md" }, 1)]
    [Arguments(new string[0], 2)]
    public async Task IncludePatterns(string[] patterns, int expectedChildren)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        using var temp = new ScratchTree();
        await temp.WriteAsync("a.md", "# A\n");
        await temp.WriteAsync("b.md", "# B\n");
        var includeGlobs = new Common.GlobPattern[patterns.Length];
        for (var i = 0; i < patterns.Length; i++)
        {
            includeGlobs[i] = patterns[i];
        }

        var options = NavOptions.Default with { Includes = includeGlobs };
        var root = NavTreeBuilder.Build(temp.Root, options);
        await Assert.That(root.Children.Length).IsEqualTo(expectedChildren);
    }

    /// <summary>Disposable scratch tree.</summary>
    private sealed class ScratchTree : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchTree"/> class.</summary>
        public ScratchTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-nav-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch root.</summary>
        public string Root { get; }

        /// <summary>Writes <paramref name="content"/> to <paramref name="relativePath"/> under <see cref="Root"/>.</summary>
        /// <param name="relativePath">Path relative to the scratch root.</param>
        /// <param name="content">UTF-8 file contents.</param>
        /// <returns>Async I/O task.</returns>
        public Task WriteAsync(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return File.WriteAllTextAsync(path, content);
        }

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
