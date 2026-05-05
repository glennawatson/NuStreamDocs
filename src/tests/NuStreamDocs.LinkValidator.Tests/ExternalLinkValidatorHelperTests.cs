// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>Direct tests for the previously private helpers in ExternalLinkValidator.</summary>
public class ExternalLinkValidatorHelperTests
{
    /// <summary>BucketByHost groups links by host, skipping URLs that aren't absolute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BucketByHostGroups()
    {
        using ScratchDir temp = new();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "page.html"),
            "<a href=\"https://a.test/x\">a</a><a href=\"https://A.test/y\">a2</a><a href=\"https://b.test/z\">b</a>");
        var corpus = await ValidationCorpus.BuildAsync(temp.Root, parallelism: 1, CancellationToken.None);
        var bucketed = ExternalLinkValidator.BucketByHost(corpus);
        await Assert.That(bucketed.ContainsKey("a.test")).IsTrue();
        await Assert.That(bucketed.ContainsKey("b.test")).IsTrue();

        // Case-insensitive grouping
        await Assert.That(bucketed["a.test"].Count).IsEqualTo(2);
    }

    /// <summary>BucketByHost on an empty corpus returns an empty map.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BucketByHostEmpty()
    {
        using ScratchDir temp = new();
        var corpus = await ValidationCorpus.BuildAsync(temp.Root, parallelism: 1, CancellationToken.None);
        await Assert.That(ExternalLinkValidator.BucketByHost(corpus).Count).IsEqualTo(0);
    }

    /// <summary>BuildPipeline returns a non-null pipeline for default options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildPipelineConstructs()
    {
        var pipeline = ExternalLinkPipelineFactory.Create(ExternalLinkValidatorOptions.Default);
        await Assert.That(pipeline).IsNotNull();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-elv-" + Guid.NewGuid().ToString("N"));
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
