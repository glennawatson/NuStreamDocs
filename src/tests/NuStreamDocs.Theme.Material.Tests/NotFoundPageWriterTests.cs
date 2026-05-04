// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>Direct render-path tests for <see cref="NotFoundPageWriter"/>.</summary>
public class NotFoundPageWriterTests
{
    /// <summary>The full skeleton renders with a stylesheet link, favicon, and site-name suffix when all are supplied.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RenderEmitsFullSkeletonWithAllArguments()
    {
        var writer = new ArrayBufferWriter<byte>();
        NotFoundPageWriter.Render(
            writer,
            "Acme Docs"u8,
            "/assets/stylesheets/theme.css"u8,
            "/assets/images/favicon.svg"u8);

        var output = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(output).Contains("<title>Page not found - Acme Docs</title>");
        await Assert.That(output).Contains("<link rel=\"stylesheet\" href=\"/assets/stylesheets/theme.css\">");
        await Assert.That(output).Contains("<link rel=\"icon\" href=\"/assets/images/favicon.svg\">");
        await Assert.That(output).Contains("<span class=\"md-ellipsis\">Acme Docs</span>");
        await Assert.That(output).Contains("Page not found");
        await Assert.That(output).Contains("&larr; Back to the home page");
    }

    /// <summary>Empty site-name omits both the title suffix and the header label.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RenderOmitsSiteNamePiecesWhenEmpty()
    {
        var writer = new ArrayBufferWriter<byte>();
        NotFoundPageWriter.Render(writer, [], "/css.css"u8, "/icon.svg"u8);

        var output = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(output).Contains("<title>Page not found</title>");
        await Assert.That(output).DoesNotContain("md-ellipsis");
    }

    /// <summary>An empty stylesheet URL skips the stylesheet link entirely.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RenderOmitsStylesheetWhenEmpty()
    {
        var writer = new ArrayBufferWriter<byte>();
        NotFoundPageWriter.Render(writer, "Site"u8, [], "/icon.svg"u8);

        var output = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(output).DoesNotContain("rel=\"stylesheet\"");
        await Assert.That(output).Contains("rel=\"icon\"");
    }

    /// <summary>An empty favicon URL skips the icon link entirely.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RenderOmitsFaviconWhenEmpty()
    {
        var writer = new ArrayBufferWriter<byte>();
        NotFoundPageWriter.Render(writer, "Site"u8, "/css.css"u8, []);

        var output = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(output).Contains("rel=\"stylesheet\"");
        await Assert.That(output).DoesNotContain("rel=\"icon\"");
    }

    /// <summary><see cref="NotFoundPageWriter.WriteIfMissingAsync"/> writes a fresh file when the target is absent.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteIfMissingCreates404Html()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smkd-404-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await NotFoundPageWriter.WriteIfMissingAsync(dir, "Site"u8.ToArray(), "/css.css"u8.ToArray(), "/icon.svg"u8.ToArray(), CancellationToken.None);
            var path = Path.Combine(dir, "404.html");
            await Assert.That(File.Exists(path)).IsTrue();
            var html = await File.ReadAllTextAsync(path);
            await Assert.That(html).Contains("Page not found");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary><see cref="NotFoundPageWriter.WriteIfMissingAsync"/> leaves an existing file untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteIfMissingDoesNotOverwrite()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smkd-404-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "404.html");
            await File.WriteAllTextAsync(path, "<!-- already there -->");
            await NotFoundPageWriter.WriteIfMissingAsync(dir, "Site"u8.ToArray(), "/css.css"u8.ToArray(), "/icon.svg"u8.ToArray(), CancellationToken.None);
            var html = await File.ReadAllTextAsync(path);
            await Assert.That(html).IsEqualTo("<!-- already there -->");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
