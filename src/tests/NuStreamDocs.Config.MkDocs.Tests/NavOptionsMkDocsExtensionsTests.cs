// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Nav;

namespace NuStreamDocs.Config.MkDocs.Tests;

/// <summary>Tests for <see cref="NavOptionsMkDocsExtensions"/>'s two <c>FromMkDocsYaml</c> overloads.</summary>
public class NavOptionsMkDocsExtensionsTests
{
    /// <summary>Number of top-level entries in the sample navigation.</summary>
    private const int EntryCount = 3;

    /// <summary>Number of pages in the sample guide section.</summary>
    private const int GuidePageCount = 2;

    /// <summary>Gets sample mkdocs.yml content with flat entries and a nested section.</summary>
    private static ReadOnlySpan<byte> SampleYaml => """
                                      site_name: Demo
                                      nav:
                                        - Home: index.md
                                        - Guide:
                                            - Intro: guide/intro.md
                                            - Setup: guide/setup.md
                                        - Reference: reference.md
                                      """u8;

    /// <summary>The byte-overload populates the curated list with the parsed nav tree.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FromMkDocsYamlBytesPopulatesCurated()
    {
        var result = NavOptions.Default.FromMkDocsYaml(SampleYaml);

        await Assert.That(result.CuratedEntries.Length).IsEqualTo(EntryCount);
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[0].Title)).IsEqualTo("Home");
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[0].Path)).IsEqualTo("index.md");
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[1].Title)).IsEqualTo("Guide");
        await Assert.That(result.CuratedEntries[1].IsSection).IsTrue();
        await Assert.That(result.CuratedEntries[1].Children.Length).IsEqualTo(GuidePageCount);
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[1].Children[0].Title)).IsEqualTo("Intro");
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[1].Children[0].Path))
            .IsEqualTo("guide/intro.md");
    }

    /// <summary>The path overload reads the file and delegates to the byte-shaped overload.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FromMkDocsYamlPathReadsFileAndPopulatesCurated()
    {
        using ScratchFile scratch = new();
        await File.WriteAllBytesAsync(scratch.Path, SampleYaml.ToArray());

        var result = NavOptions.Default.FromMkDocsYaml((FilePath)scratch.Path);

        await Assert.That(result.CuratedEntries.Length).IsEqualTo(EntryCount);
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[2].Title)).IsEqualTo("Reference");
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[2].Path)).IsEqualTo("reference.md");
    }

    /// <summary>Empty / whitespace path trips the argument-validation guard before any disk I/O.</summary>
    /// <param name="path">Invalid path candidate.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task FromMkDocsYamlPathRejectsBlank(string path) =>
        await Assert.That(() => NavOptions.Default.FromMkDocsYaml((FilePath)path)).Throws<ArgumentException>();

    /// <summary>Yaml without a <c>nav:</c> key yields an empty curated list rather than throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FromMkDocsYamlWithoutNavReturnsEmpty()
    {
        var result = NavOptions.Default.FromMkDocsYaml("site_name: Demo\n"u8);

        await Assert.That(result.CuratedEntries.Length).IsEqualTo(0);
    }

    /// <summary>Disposable scratch file the test cleans up.</summary>
    private sealed class ScratchFile : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchFile"/> class.</summary>
        public ScratchFile() =>
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"smd-mkdocs-nav-{Guid.NewGuid():N}.yml");

        /// <summary>Gets the absolute file path the test writes into.</summary>
        public string Path { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }
}
