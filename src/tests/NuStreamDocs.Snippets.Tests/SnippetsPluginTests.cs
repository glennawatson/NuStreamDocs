// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

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
    public async Task PreprocessPassesThroughBeforeConfigure()
    {
        var plugin = new SnippetsPlugin();
        var sink = new ArrayBufferWriter<byte>(32);
        plugin.Preprocess("hello"u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("hello");
    }

    /// <summary>OnConfigureAsync captures the input root when no override is supplied.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnConfigureCapturesInputRoot()
    {
        using var temp = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "include.md"), "spliced");

        var plugin = new SnippetsPlugin();
        await plugin.OnConfigureAsync(new(temp.Root, "/out", []), CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(64);
        plugin.Preprocess("--8<-- \"include.md\""u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("spliced");
    }

    /// <summary>Explicit base-directory override wins over the input root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExplicitBaseDirectoryWins()
    {
        using var temp = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "include.md"), "explicit");

        var plugin = new SnippetsPlugin(temp.Root);
        await plugin.OnConfigureAsync(new("/wrong", "/out", []), CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(64);
        plugin.Preprocess("--8<-- \"include.md\""u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("explicit");
    }

    /// <summary>Preprocess rejects a null sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessRejectsNullSink()
    {
        var plugin = new SnippetsPlugin();
        var ex = Assert.Throws<ArgumentNullException>(() => plugin.Preprocess(default, null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseSnippets() registers the default-base plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSnippetsRegisters()
    {
        var builder = new DocBuilder();
        await Assert.That(builder.UseSnippets()).IsSameReferenceAs(builder);
    }

    /// <summary>UseSnippets(baseDir) registers the override-base plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSnippetsWithBaseRegisters()
    {
        var builder = new DocBuilder();
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
        var builder = new DocBuilder();
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
