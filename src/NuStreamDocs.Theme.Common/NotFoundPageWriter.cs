// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Theme.Common;

/// <summary>Writes a default <c>site/404.html</c> when none was authored.</summary>
/// <remarks>
/// Self-contained HTML that re-uses the theme's stylesheet + favicon URLs so the not-found
/// page picks up the same color scheme and chrome. The header carries just the site name
/// and a home-link; the body is a centred message. Static hosts (GitHub Pages, Netlify,
/// S3 static-website) honour <c>/404.html</c> for not-found responses.
/// </remarks>
public static class NotFoundPageWriter
{
    /// <summary>The relative output path the 404 page is written to.</summary>
    private const string NotFoundFileName = "404.html";

    /// <summary>Writes <c>404.html</c> at <paramref name="outputRoot"/> when one isn't already on disk.</summary>
    /// <param name="outputRoot">Absolute site output root.</param>
    /// <param name="siteName">UTF-8 top-bar site name; empty when the user didn't set one.</param>
    /// <param name="stylesheetUrl">UTF-8 site-rooted URL of the theme stylesheet.</param>
    /// <param name="faviconUrl">UTF-8 site-rooted URL of the favicon; empty when none.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async task.</returns>
    public static ValueTask WriteIfMissingAsync(
        DirectoryPath outputRoot,
        byte[] siteName,
        byte[] stylesheetUrl,
        byte[] faviconUrl,
        CancellationToken cancellationToken)
    {
        if (outputRoot.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        var target = Path.Combine(outputRoot.Value, NotFoundFileName);
        if (File.Exists(target))
        {
            return ValueTask.CompletedTask;
        }

        var writer = new ArrayBufferWriter<byte>(2048);
        WriteDocument(writer, siteName, stylesheetUrl, faviconUrl);
        Directory.CreateDirectory(outputRoot.Value);
        return new(File.WriteAllBytesAsync(target, writer.WrittenMemory, cancellationToken));
    }

    /// <summary>Pure renderer — writes the 404 HTML into <paramref name="writer"/> without touching disk. Exposed for tests.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="siteName">UTF-8 site-name bytes; empty omits the suffix in the title and the header label.</param>
    /// <param name="stylesheetUrl">UTF-8 site-rooted URL of the theme stylesheet; empty omits the link.</param>
    /// <param name="faviconUrl">UTF-8 site-rooted URL of the favicon; empty omits the link.</param>
    public static void Render(
        ArrayBufferWriter<byte> writer,
        ReadOnlySpan<byte> siteName,
        ReadOnlySpan<byte> stylesheetUrl,
        ReadOnlySpan<byte> faviconUrl)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteDocument(writer, siteName, stylesheetUrl, faviconUrl);
    }

    /// <summary>Streams the 404 HTML into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="siteName">Site name bytes.</param>
    /// <param name="stylesheetUrl">Theme stylesheet URL bytes.</param>
    /// <param name="faviconUrl">Favicon URL bytes.</param>
    private static void WriteDocument(
        ArrayBufferWriter<byte> writer,
        ReadOnlySpan<byte> siteName,
        ReadOnlySpan<byte> stylesheetUrl,
        ReadOnlySpan<byte> faviconUrl)
    {
        ReadOnlySpan<byte> head = """
            <!doctype html>
            <html lang="en" class="no-js">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Page not found
            """u8;
        Write(writer, head);
        if (!siteName.IsEmpty)
        {
            Write(writer, " - "u8);
            writer.Write(siteName);
        }

        Write(writer, "</title>\n"u8);
        if (!stylesheetUrl.IsEmpty)
        {
            Write(writer, "  <link rel=\"stylesheet\" href=\""u8);
            writer.Write(stylesheetUrl);
            Write(writer, "\">\n"u8);
        }

        if (!faviconUrl.IsEmpty)
        {
            Write(writer, "  <link rel=\"icon\" href=\""u8);
            writer.Write(faviconUrl);
            Write(writer, "\">\n"u8);
        }

        ReadOnlySpan<byte> chromeOpen = """
            </head>
            <body data-md-color-scheme="default">
              <div class="md-container">
                <header class="md-header">
                  <nav class="md-header__inner md-grid" aria-label="Header">
                    <a href="/" class="md-header__button md-logo" aria-label="Home">
            """u8;
        Write(writer, chromeOpen);
        if (!siteName.IsEmpty)
        {
            Write(writer, "<span class=\"md-ellipsis\">"u8);
            writer.Write(siteName);
            Write(writer, "</span>"u8);
        }

        ReadOnlySpan<byte> body = """
            </a>
                  </nav>
                </header>
                <main class="md-main">
                  <div class="md-main__inner md-grid" style="grid-template-columns: minmax(0, 1fr)">
                    <article class="md-content__inner md-typeset" id="md-content" style="text-align:center;padding:4rem 1rem;max-width:36rem;margin:0 auto">
                      <h1>Page not found</h1>
                      <p>Oops &mdash; the page you were looking for doesn&rsquo;t exist (or has moved).</p>
                      <p><a href="/">&larr; Back to the home page</a></p>
                    </article>
                  </div>
                </main>
              </div>
            </body>
            </html>
            """u8;
        Write(writer, body);
    }

    /// <summary>Copies <paramref name="bytes"/> into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to append.</param>
    private static void Write(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        bytes.CopyTo(writer.GetSpan(bytes.Length));
        writer.Advance(bytes.Length);
    }
}
