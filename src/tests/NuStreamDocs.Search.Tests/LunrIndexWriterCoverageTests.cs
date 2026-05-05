// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Tests;

/// <summary>Coverage for the LunrIndexWriter overloads.</summary>
public class LunrIndexWriterCoverageTests
{
    /// <summary>Three-arg Write emits a JSON file with no extra stopwords.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteSimpleOverload()
    {
        using TempDir dir = new();
        var path = Path.Combine(dir.Root, "lunr.json");
        LunrIndexWriter.Write(path, "en", [new([.. "/a.html"u8], [.. "A"u8], [.. "body"u8])]);
        await Assert.That(File.Exists(path)).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(path)).Contains("\"lang\":\"en\"");
    }

    /// <summary>An empty language defaults to "en".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyLanguageDefaultsToEn()
    {
        using TempDir dir = new();
        var path = Path.Combine(dir.Root, "lunr.json");
        LunrIndexWriter.Write(path, string.Empty, [new([.. "/a.html"u8], [.. "A"u8], [.. "B"u8])]);
        await Assert.That(await File.ReadAllTextAsync(path)).Contains("\"lang\":\"en\"");
    }

    /// <summary>Extra stopwords appear in the config block as an array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExtraStopwordsEmittedWhenProvided()
    {
        using TempDir dir = new();
        var path = Path.Combine(dir.Root, "lunr.json");
        LunrIndexWriter.Write(path, "en", [new([.. "/a.html"u8], [.. "A"u8], [.. "B"u8])], [[.. "foo"u8], [.. "bar"u8]]);
        var json = await File.ReadAllTextAsync(path);
        await Assert.That(json).Contains("\"extra_stopwords\"");
        await Assert.That(json).Contains("\"foo\"");
        await Assert.That(json).Contains("\"bar\"");
    }

    /// <summary>Multiple documents are emitted in order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleDocumentsEmitted()
    {
        using TempDir dir = new();
        var path = Path.Combine(dir.Root, "lunr.json");
        LunrIndexWriter.Write(
            path,
            "en",
            [
                new([.. "/one.html"u8], [.. "T1"u8], [.. "X"u8]),
                new([.. "/two.html"u8], [.. "T2"u8], [.. "Y"u8])
            ]);
        var json = await File.ReadAllTextAsync(path);
        await Assert.That(json).Contains("/one.html");
        await Assert.That(json).Contains("/two.html");
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-lunr-" + Guid.NewGuid().ToString("N"));
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
