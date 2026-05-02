// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Caching;

namespace NuStreamDocs.Tests;

/// <summary>Parameterized tests for BuildManifest.LoadAsync's parse-rejection branches.</summary>
public class BuildManifestParseTests
{
    /// <summary>Parameterized cases covering every shape that must yield an empty manifest.</summary>
    /// <param name="json">On-disk manifest contents.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("{}")]
    [Arguments("{\"schema\":\"bad\",\"entries\":[]}")]
    [Arguments("{\"schema\":99,\"entries\":[]}")]
    [Arguments("{\"schema\":1,\"entries\":\"not an array\"}")]
    [Arguments("{\"schema\":1,\"entries\":[]}")]
    [Arguments("{\"schema\":1,\"entries\":[{}]}")]
    [Arguments("{\"schema\":1,\"entries\":[{\"path\":\"a\"}]}")]
    [Arguments("{\"schema\":1,\"entries\":[{\"hash\":\"a\"}]}")]
    [Arguments("{\"schema\":1,\"entries\":[{\"path\":null,\"hash\":\"x\",\"len\":0}]}")]
    [Arguments("{\"schema\":1,\"entries\":[\"not an object\"]}")]
    public async Task RejectedShapesReturnEmpty(string json)
    {
        using var temp = new ScratchDir();
        var path = Path.Combine(temp.Root, BuildManifest.FileName);
        await File.WriteAllTextAsync(path, json);
        var manifest = await BuildManifest.LoadAsync(temp.Root, CancellationToken.None);
        await Assert.That(manifest.Count).IsEqualTo(0);
    }

    /// <summary>A valid manifest with mixed good/bad entries keeps only the good ones.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SkipsBadEntriesKeepsGood()
    {
        using var temp = new ScratchDir();
        var path = Path.Combine(temp.Root, BuildManifest.FileName);
        const string Json = "{\"schema\":1,\"entries\":[" +
            "{\"path\":\"good.md\",\"hash\":\"h\",\"len\":1}," +
            "{\"path\":\"missing-hash.md\",\"len\":2}," +
            "\"not an object\"," +
            "{\"path\":\"second-good.md\",\"hash\":\"h2\",\"len\":3}]}";
        await File.WriteAllTextAsync(path, Json);
        var manifest = await BuildManifest.LoadAsync(temp.Root, CancellationToken.None);
        await Assert.That(manifest.Count).IsEqualTo(2);
        await Assert.That(manifest.TryGet("good.md", out _)).IsTrue();
        await Assert.That(manifest.TryGet("second-good.md", out _)).IsTrue();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-bm2-" + Guid.NewGuid().ToString("N"));
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
