// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Snippets.Tests;

/// <summary>Behavior tests for <c>SnippetsRewriter</c>.</summary>
public class SnippetsRewriterTests
{
    /// <summary>A simple include splices the file contents inline.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SimpleIncludeSplicesFile()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("intro.md", "Hello from snippet.\n");
        const string Source = "Before.\n--8<-- \"intro.md\"\nAfter.";
        var result = fixture.Rewrite(Source);
        await Assert.That(result).IsEqualTo("Before.\nHello from snippet.\nAfter.");
    }

    /// <summary>Recursive includes expand transitively.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RecursiveIncludeExpands()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("a.md", "A then\n--8<-- \"b.md\"\n");
        fixture.Write("b.md", "B body\n");
        var result = fixture.Rewrite("--8<-- \"a.md\"\n");
        await Assert.That(result).IsEqualTo("A then\nB body\n");
    }

    /// <summary>A self-referencing snippet renders an error rather than recursing forever.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CycleProducesErrorBlock()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("loop.md", "loop\n--8<-- \"loop.md\"\n");
        var result = fixture.Rewrite("--8<-- \"loop.md\"\n");
        await Assert.That(result).Contains("snippet cycle detected");
    }

    /// <summary>Missing snippet produces an error block instead of silent omission.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingSnippetProducesErrorBlock()
    {
        using var fixture = new SnippetFixture();
        var result = fixture.Rewrite("--8<-- \"nope.md\"\n");
        await Assert.That(result).Contains("snippet not found");
    }

    /// <summary>Paths that resolve outside the base directory are refused.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PathEscapeIsRefused()
    {
        using var fixture = new SnippetFixture();
        var result = fixture.Rewrite("--8<-- \"../escape.md\"\n");
        await Assert.That(result).Contains("snippet path escapes base directory");
    }

    /// <summary>An indented marker still triggers the include.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndentedMarkerStillTriggers()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("foo.md", "X");
        var result = fixture.Rewrite("    --8<-- \"foo.md\"\n");
        await Assert.That(result).IsEqualTo("X");
    }

    /// <summary>Source with no markers round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        using var fixture = new SnippetFixture();
        const string Source = "No includes here at all.";
        await Assert.That(fixture.Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput()
    {
        using var fixture = new SnippetFixture();
        await Assert.That(fixture.Rewrite(string.Empty)).IsEqualTo(string.Empty);
    }

    /// <summary>Disposable scratch directory + helper that drives the rewriter against it.</summary>
    private sealed class SnippetFixture : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnippetFixture"/> class.
        /// </summary>
        public SnippetFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-snippets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the temporary base directory.</summary>
        public string Root { get; }

        /// <summary>Writes <paramref name="content"/> to <paramref name="relativePath"/> under the fixture root.</summary>
        /// <param name="relativePath">Path relative to <c>Root</c>.</param>
        /// <param name="content">UTF-16 content.</param>
        public void Write(string relativePath, string content)
        {
            var absolute = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, content);
        }

        /// <summary>Drives the rewriter against the fixture root.</summary>
        /// <param name="source">UTF-16 source.</param>
        /// <returns>Rewritten text.</returns>
        public string Rewrite(string source)
        {
            var bytes = Encoding.UTF8.GetBytes(source);
            var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
            var cache = new Dictionary<byte[], byte[]>(ByteArrayComparer.Instance);
            SnippetsRewriter.Rewrite(bytes, Root, cache, sink);
            return Encoding.UTF8.GetString(sink.WrittenSpan);
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
                // Already deleted — nothing to clean up.
            }
        }
    }
}
