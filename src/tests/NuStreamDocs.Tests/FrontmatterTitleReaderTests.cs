// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end tests for the front-matter title peek used by the nav builders.</summary>
public class FrontmatterTitleReaderTests
{
    /// <summary>Lower-case <c>title:</c> resolves through both the string and byte entry points.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsLowerCaseTitle()
    {
        using var fixture = await TempFile.WriteAsync("---\ntitle: Hello\n---\n# Body\n");
        await Assert.That(FrontmatterTitleReader.Read(fixture.Path)).IsEqualTo("Hello");
        var bytes = FrontmatterTitleReader.ReadBytes((FilePath)fixture.Path);
        await Assert.That(bytes is not null && Encoding.UTF8.GetString(bytes!) == "Hello").IsTrue();
    }

    /// <summary>Capitalised <c>Title:</c> (Wyam convention) is honoured when no lower-case key is present.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FallsBackToCapitalisedTitle()
    {
        using var fixture = await TempFile.WriteAsync("---\nTitle: Capitalised\n---\nbody");
        await Assert.That(FrontmatterTitleReader.Read(fixture.Path)).IsEqualTo("Capitalised");
    }

    /// <summary>Quoted titles are stripped of their surrounding YAML quotes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StripsYamlQuotes()
    {
        using var doubleQuoted = await TempFile.WriteAsync("---\ntitle: \"Quoted\"\n---\n");
        using var singleQuoted = await TempFile.WriteAsync("---\ntitle: 'Sing'\n---\n");
        await Assert.That(FrontmatterTitleReader.Read(doubleQuoted.Path)).IsEqualTo("Quoted");
        await Assert.That(FrontmatterTitleReader.Read(singleQuoted.Path)).IsEqualTo("Sing");
    }

    /// <summary>Files with no front-matter or no title key return null without throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsNullWhenAbsent()
    {
        using var noFrontmatter = await TempFile.WriteAsync("# Body only");
        using var emptyTitle = await TempFile.WriteAsync("---\nlayout: page\n---\nbody");
        await Assert.That(FrontmatterTitleReader.Read(noFrontmatter.Path)).IsNull();
        await Assert.That(FrontmatterTitleReader.Read(emptyTitle.Path)).IsNull();
    }

    /// <summary>An empty file returns null and never throws.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsNullForEmptyFile()
    {
        using var empty = await TempFile.WriteAsync(string.Empty);
        await Assert.That(FrontmatterTitleReader.Read(empty.Path)).IsNull();
        await Assert.That(FrontmatterTitleReader.ReadBytes((FilePath)empty.Path)).IsNull();
    }

    /// <summary>A missing path returns null without throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsNullForMissingPath()
    {
        var missing = Path.Combine(Path.GetTempPath(), "smkd-nofile-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        await Assert.That(FrontmatterTitleReader.Read(missing)).IsNull();
        await Assert.That(FrontmatterTitleReader.ReadBytes((FilePath)missing)).IsNull();
    }

    /// <summary>Both string-shaped <see cref="FrontmatterTitleReader.Read"/> and byte-shaped <c>ReadBytes</c> reject empty paths.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyPathsThrow()
    {
        await Assert.That(() => FrontmatterTitleReader.Read(string.Empty)).Throws<ArgumentException>();
        await Assert.That(() => FrontmatterTitleReader.ReadBytes((FilePath)string.Empty)).Throws<ArgumentException>();
    }

    /// <summary>Disposable temp file with the supplied UTF-8 contents.</summary>
    private sealed class TempFile : IDisposable
    {
        /// <summary>UTF-8 encoder that does NOT prepend a byte-order mark — matches how mkdocs-style frontmatter is authored.</summary>
        private static readonly UTF8Encoding NoBom = new(encoderShouldEmitUTF8Identifier: false);

        /// <summary>Initializes a new instance of the <see cref="TempFile"/> class.</summary>
        /// <param name="path">Absolute path to the file.</param>
        private TempFile(string path) => Path = path;

        /// <summary>Gets the absolute path to the file.</summary>
        public string Path { get; }

        /// <summary>Writes <paramref name="contents"/> to a fresh temp file and returns a fixture wrapping it.</summary>
        /// <param name="contents">UTF-8 contents.</param>
        /// <returns>The fixture; caller disposes.</returns>
        public static async Task<TempFile> WriteAsync(string contents)
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "smkd-fmt-tests");
            Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".md");

            // Explicit no-BOM UTF-8 — Encoding.UTF8 prepends a 3-byte BOM that prod files don't have.
            await File.WriteAllTextAsync(path, contents, NoBom).ConfigureAwait(false);
            return new(path);
        }

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
                // best-effort cleanup
            }
        }
    }
}
