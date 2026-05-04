// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end tests for the front-matter <c>Order:</c> integer reader.</summary>
public class FrontmatterOrderReaderTests
{
    /// <summary>Capitalised <c>Order:</c> (Statiq convention) parses cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsCapitalisedOrder()
    {
        using var fixture = await TempFile.WriteAsync("---\nOrder: 7\n---\n# Body\n");
        await Assert.That(FrontmatterOrderReader.TryRead(fixture.Path, out var order) && order == 7).IsTrue();
    }

    /// <summary>Lower-case <c>order:</c> is honoured as a fallback.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsLowerCaseOrder()
    {
        using var fixture = await TempFile.WriteAsync("---\norder: 3\n---\n# Body\n");
        await Assert.That(FrontmatterOrderReader.TryRead(fixture.Path, out var order) && order == 3).IsTrue();
    }

    /// <summary>Negative integers are accepted (so an item can sort above the default zero).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsNegativeOrder()
    {
        using var fixture = await TempFile.WriteAsync("---\nOrder: -2\n---\n# Body\n");
        await Assert.That(FrontmatterOrderReader.TryRead(fixture.Path, out var order) && order == -2).IsTrue();
    }

    /// <summary>Pages without an <c>Order:</c> key short-circuit to false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsFalseWhenAbsent()
    {
        using var fixture = await TempFile.WriteAsync("---\ntitle: Hello\n---\n# Body\n");
        await Assert.That(FrontmatterOrderReader.TryRead(fixture.Path, out _)).IsFalse();
    }

    /// <summary>Non-integer scalars (e.g. quoted strings) are rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsFalseForNonInteger()
    {
        using var fixture = await TempFile.WriteAsync("---\nOrder: not-a-number\n---\n# Body\n");
        await Assert.That(FrontmatterOrderReader.TryRead(fixture.Path, out _)).IsFalse();
    }

    /// <summary>Missing files return false without throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsFalseForMissingFile()
    {
        var missing = Path.Combine(Path.GetTempPath(), "smkd-nofile-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        await Assert.That(FrontmatterOrderReader.TryRead((FilePath)missing, out _)).IsFalse();
    }

    /// <summary>Empty paths short-circuit to false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsFalseForEmptyPath() =>
        await Assert.That(FrontmatterOrderReader.TryRead((FilePath)string.Empty, out _)).IsFalse();

    /// <summary>Disposable temp file using a no-BOM UTF-8 encoder so the YAML opener probe matches.</summary>
    private sealed class TempFile : IDisposable
    {
        /// <summary>UTF-8 encoder that does NOT prepend a byte-order mark.</summary>
        private static readonly UTF8Encoding NoBom = new(encoderShouldEmitUTF8Identifier: false);

        /// <summary>Initializes a new instance of the <see cref="TempFile"/> class.</summary>
        /// <param name="path">Absolute path to the file.</param>
        private TempFile(string path) => Path = path;

        /// <summary>Gets the absolute path to the file.</summary>
        public string Path { get; }

        /// <summary>Writes <paramref name="contents"/> to a fresh temp file (no BOM) and returns a fixture wrapping it.</summary>
        /// <param name="contents">UTF-8 contents.</param>
        /// <returns>The fixture; caller disposes.</returns>
        public static async Task<TempFile> WriteAsync(string contents)
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "smkd-order-tests");
            Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".md");
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
