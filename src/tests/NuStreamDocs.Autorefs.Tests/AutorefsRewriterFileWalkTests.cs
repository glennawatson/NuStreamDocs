// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>End-to-end file-walk tests for <c>AutorefsRewriter</c>.</summary>
public class AutorefsRewriterFileWalkTests
{
    /// <summary>RewriteAll resolves markers across files in a temp directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteAllResolves()
    {
        using ScratchDir temp = new();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "page.html"), "see <a href=\"@autoref:Foo\">Foo</a>");

        AutorefsRegistry registry = new();
        registry.Register("Foo"u8, [.. "/api/foo.html"u8], fragment: default);

        var count = AutorefsRewriter.RewriteAll(temp.Root, registry);
        await Assert.That(count).IsEqualTo(1);
        var html = await File.ReadAllTextAsync(Path.Combine(temp.Root, "page.html"));
        await Assert.That(html).Contains("/api/foo.html");
    }

    /// <summary>RewriteAll with logger reports resolved + missing counts.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteAllLoggedReportsCounts()
    {
        using ScratchDir temp = new();
        var pagePath = Path.Combine(temp.Root, "page.html");
        await File.WriteAllTextAsync(pagePath, "<a href=\"@autoref:Resolved\">x</a> and <a href=\"@autoref:Missing\">y</a>");

        AutorefsRegistry registry = new();
        registry.Register("Resolved"u8, [.. "/r.html"u8], fragment: default);

        var (resolved, missing) = AutorefsRewriter.RewriteAll(temp.Root, registry, NullLogger.Instance);
        await Assert.That(resolved).IsEqualTo(1);
        await Assert.That(missing).IsEqualTo(1);
    }

    /// <summary>RewriteAll on a non-existent directory returns zero.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteAllMissingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "smkd-ar-missing-" + Guid.NewGuid().ToString("N"));
        await Assert.That(AutorefsRewriter.RewriteAll(path, new())).IsEqualTo(0);
    }

    /// <summary>RewriteAll(logger) on a non-existent directory returns zeros.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteAllLoggedMissingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "smkd-ar-missing-" + Guid.NewGuid().ToString("N"));
        var (r, m) = AutorefsRewriter.RewriteAll(path, new(), NullLogger.Instance);
        await Assert.That(r).IsEqualTo(0);
        await Assert.That(m).IsEqualTo(0);
    }

    /// <summary>RewriteOne returns false when the source contains no markers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteOneNoMarkers()
    {
        using ScratchDir temp = new();
        var path = Path.Combine(temp.Root, "plain.html");
        await File.WriteAllTextAsync(path, "<p>plain</p>");
        await Assert.That(AutorefsRewriter.RewriteOne(path, new())).IsFalse();
    }

    /// <summary>RewriteOne returns false when the markers don't resolve (file unchanged).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteOneAllMissing()
    {
        using ScratchDir temp = new();
        var path = Path.Combine(temp.Root, "missing.html");
        await File.WriteAllTextAsync(path, "<a href=\"@autoref:NotInRegistry\">x</a>");
        await Assert.That(AutorefsRewriter.RewriteOne(path, new())).IsFalse();
    }

    /// <summary>RewriteOne returns true when at least one marker resolves.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteOneResolves()
    {
        using ScratchDir temp = new();
        var path = Path.Combine(temp.Root, "good.html");
        await File.WriteAllTextAsync(path, "<a href=\"@autoref:Foo\">x</a>");
        AutorefsRegistry registry = new();
        registry.Register("Foo"u8, [.. "/foo.html"u8], fragment: default);
        await Assert.That(AutorefsRewriter.RewriteOne(path, registry)).IsTrue();
        var rewritten = await File.ReadAllTextAsync(path);
        await Assert.That(rewritten).Contains("/foo.html");
    }

    /// <summary>RewriteOne rejects empty path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteOneRejectsEmptyPath()
    {
        var ex = Assert.Throws<ArgumentException>(static () =>
            AutorefsRewriter.RewriteOne(string.Empty, new()));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>RewriteAll rejects empty path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteAllRejectsEmptyPath()
    {
        var ex = Assert.Throws<ArgumentException>(static () =>
            AutorefsRewriter.RewriteAll(string.Empty, new()));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>RewriteAll rejects null registry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewriteAllRejectsNullRegistry()
    {
        using ScratchDir temp = new();
        var root = temp.Root;
        var ex = Assert.Throws<ArgumentNullException>(() => AutorefsRewriter.RewriteAll(root, null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-ar-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch directory.</summary>
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
