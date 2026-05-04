// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Sitemap;

/// <summary>
/// Emits a default <c>404.html</c> at the site root in
/// <see cref="OnFinalizeAsync"/> when no <c>404.html</c> already exists
/// — typically because the docs tree has no <c>404.md</c> source.
/// Sites that ship their own <c>404.md</c> will see that page
/// already in the output and this plugin no-ops.
/// </summary>
public sealed class NotFoundPlugin : IDocPlugin
{
    /// <summary>UTF-8 bytes of the default 404 document; styled as a minimal centred page.</summary>
    private static readonly byte[] DefaultDocument =
        [.. """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>404 — Page not found</title>
<meta name="robots" content="noindex">
<style>
  body { font: 16px/1.5 system-ui, sans-serif; margin: 0; min-height: 100vh; display: grid; place-items: center; background: #fff; color: #222; }
  main { text-align: center; padding: 2rem; }
  h1 { font-size: 4rem; margin: 0 0 0.5rem; }
  p { margin: 0.25rem 0; }
  a { color: #0366d6; }
</style>
</head>
<body>
<main>
  <h1>404</h1>
  <p>The page you were looking for could not be found.</p>
  <p><a href="/">Return to the homepage</a></p>
</main>
</body>
</html>
"""u8];

    /// <inheritdoc/>
    public byte[] Name => "404"u8.ToArray();

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        var path = Path.Combine(context.OutputRoot, "404.html");
        if (File.Exists(path))
        {
            return;
        }

        await File.WriteAllBytesAsync(path, DefaultDocument, cancellationToken).ConfigureAwait(false);
    }
}
