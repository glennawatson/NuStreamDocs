// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Optimise.Tests;

/// <summary>End-to-end tests for <c>OptimisePlugin</c>.</summary>
public class OptimisePluginTests
{
    /// <summary>HTML files above the minimum size get gzip + brotli siblings.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsGzipAndBrotliSiblings()
    {
        var dir = TempDir();
        try
        {
            var page = Path.Combine(dir, "page.html");
            await File.WriteAllTextAsync(page, new string('x', 8192));

            var plugin = new OptimisePlugin(OptimiseOptions.Default);
            await plugin.CompressTreeAsync(dir, CancellationToken.None);

            await Assert.That(File.Exists(page + ".gz")).IsTrue();
            await Assert.That(File.Exists(page + ".br")).IsTrue();
            await Assert.That(new FileInfo(page + ".gz").Length).IsLessThan(new FileInfo(page).Length);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Files smaller than the configured minimum are skipped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsFilesBelowMinimum()
    {
        var dir = TempDir();
        try
        {
            var tiny = Path.Combine(dir, "tiny.html");
            await File.WriteAllTextAsync(tiny, "small");

            var plugin = new OptimisePlugin(OptimiseOptions.Default);
            await plugin.CompressTreeAsync(dir, CancellationToken.None);

            await Assert.That(File.Exists(tiny + ".gz")).IsFalse();
            await Assert.That(File.Exists(tiny + ".br")).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Already-compressed sibling files are not re-compressed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsAlreadyCompressedSiblings()
    {
        var dir = TempDir();
        try
        {
            var page = Path.Combine(dir, "page.html");
            await File.WriteAllTextAsync(page, new string('y', 4096));
            await File.WriteAllTextAsync(page + ".gz", "preexisting");

            var plugin = new OptimisePlugin(OptimiseOptions.Default with { Formats = OptimiseFormats.Gzip, MinimumBytes = 1024 });
            await plugin.CompressTreeAsync(dir, CancellationToken.None);

            // .gz sibling will be overwritten via .gz of the source — but we never iterate into the .gz itself.
            await Assert.That(File.Exists(page + ".gz")).IsTrue();
            await Assert.That(File.Exists(page + ".gz.gz")).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Builds a unique temp directory.</summary>
    /// <returns>Absolute path.</returns>
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smd-opt-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
