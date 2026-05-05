// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Snippets.Tests;

/// <summary>Lifecycle / registration tests for <c>SnippetsPlugin</c>.</summary>
public class SnippetsPluginTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new SnippetsPlugin().Name.SequenceEqual("snippets"u8)).IsTrue();

    /// <summary>Without configure being called the source passes through untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreRenderPassesThroughBeforeConfigure()
    {
        SnippetsPlugin plugin = new();
        ArrayBufferWriter<byte> sink = new(32);
        PagePreRenderContext ctx = new("p.md", "hello"u8, sink);
        plugin.PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("hello");
    }

    /// <summary>ConfigureAsync captures the input root when no override is supplied.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureCapturesInputRoot()
    {
        using TempDir temp = new();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "include.md"), "spliced");

        SnippetsPlugin plugin = new();
        await plugin.ConfigureAsync(new(temp.Root, "/out", [], new()), CancellationToken.None);

        ArrayBufferWriter<byte> sink = new(64);
        PagePreRenderContext ctx = new("page.md", "--8<-- \"include.md\""u8, sink);
        plugin.PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("spliced");
    }

    /// <summary>Explicit base-directory override wins over the input root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExplicitBaseDirectoryWins()
    {
        using TempDir temp = new();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "include.md"), "explicit");

        SnippetsPlugin plugin = new(temp.Root);
        await plugin.ConfigureAsync(new("/wrong", "/out", [], new()), CancellationToken.None);

        ArrayBufferWriter<byte> sink = new(64);
        PagePreRenderContext ctx = new("page.md", "--8<-- \"include.md\""u8, sink);
        plugin.PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("explicit");
    }

    /// <summary>UseSnippets() registers the default-base plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSnippetsRegisters()
    {
        DocBuilder builder = new();
        await Assert.That(builder.UseSnippets()).IsSameReferenceAs(builder);
    }

    /// <summary>UseSnippets(baseDir) registers the override-base plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSnippetsWithBaseRegisters()
    {
        DocBuilder builder = new();
        await Assert.That(builder.UseSnippets("/some/path")).IsSameReferenceAs(builder);
    }

    /// <summary>UseSnippets rejects a null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSnippetsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderSnippetsExtensions.UseSnippets(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseSnippets(baseDir) rejects a whitespace base directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSnippetsRejectsWhitespaceBase()
    {
        DocBuilder builder = new();
        var ex = Assert.Throws<ArgumentException>(() => builder.UseSnippets("   "));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-sn-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch directory.</summary>
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
                // Already gone.
            }
        }
    }
}
