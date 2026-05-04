// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Serve.Logging;

namespace NuStreamDocs.Serve;

/// <summary>
/// Builder-extension surface for the watch / dev-server pipeline.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// await new DocBuilder()
///     .WithInput("docs").WithOutput("site")
///     .UseMaterialTheme().UseNav()
///     .WatchAndServeAsync();
/// </code>
/// The call returns when the supplied cancellation token is triggered.
/// </remarks>
public static class DocBuilderServeExtensions
{
    /// <summary>Runs an initial build, starts the dev server, then loops on file-system changes — rebuilding and signaling connected browsers.</summary>
    /// <param name="builder">Configured builder.</param>
    /// <returns>Async task that completes when the loop exits.</returns>
    public static Task WatchAndServeAsync(this DocBuilder builder) =>
        builder.WatchAndServeAsync(WatchAndServeOptions.Default, NullLogger.Instance, CancellationToken.None);

    /// <summary>Runs the watch + serve loop with explicit cancellation.</summary>
    /// <param name="builder">Configured builder.</param>
    /// <param name="cancellationToken">Cancellation token; cancellation triggers graceful shutdown.</param>
    /// <returns>Async task.</returns>
    public static Task WatchAndServeAsync(this DocBuilder builder, in CancellationToken cancellationToken) =>
        builder.WatchAndServeAsync(WatchAndServeOptions.Default, NullLogger.Instance, cancellationToken);

    /// <summary>Runs the watch + serve loop with options-customization.</summary>
    /// <param name="builder">Configured builder.</param>
    /// <param name="configure">Function that receives <see cref="WatchAndServeOptions.Default"/> and returns the customized set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async task.</returns>
    public static Task WatchAndServeAsync(this DocBuilder builder, Func<WatchAndServeOptions, WatchAndServeOptions> configure, in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return builder.WatchAndServeAsync(configure(WatchAndServeOptions.Default), NullLogger.Instance, cancellationToken);
    }

    /// <summary>Runs the watch + serve loop with options + logger.</summary>
    /// <param name="builder">Configured builder.</param>
    /// <param name="configure">Options customization.</param>
    /// <param name="logger">Logger that receives lifecycle events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async task.</returns>
    public static Task WatchAndServeAsync(this DocBuilder builder, Func<WatchAndServeOptions, WatchAndServeOptions> configure, ILogger logger, in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return builder.WatchAndServeAsync(configure(WatchAndServeOptions.Default), logger, cancellationToken);
    }

    /// <summary>Most-specific overload — every other entry point delegates here.</summary>
    /// <param name="builder">Configured builder.</param>
    /// <param name="options">Resolved watch + serve options.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async task.</returns>
    public static async Task WatchAndServeAsync(this DocBuilder builder, WatchAndServeOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(logger);

        // Link the supplied token with an internal Ctrl+C handler. Callers that pass CancellationToken.None
        // (e.g. Nuke's Serve target) still need an exit path on console interrupt — without this the
        // `await foreach` over the watcher never observes cancellation and the process hangs.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var combinedToken = linkedCts.Token;
        ConsoleCancelEventHandler? consoleHandler = (_, args) =>
        {
            args.Cancel = true;
            try
            {
                linkedCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Cancellation source already torn down; nothing to do.
            }
        };
        Console.CancelKeyPress += consoleHandler;

        try
        {
            // Initial build: synchronous in the lifecycle so the host has something to serve immediately.
            await builder.BuildAsync(combinedToken).ConfigureAwait(false);

            var broker = new LiveReloadBroker();
            var app = await DevServer.StartAsync(builder.OutputRoot, options, broker, combinedToken).ConfigureAwait(false);
            var url = DevServer.BuildUrl(options);
            ServeLoggingHelper.LogServerStart(logger, url, builder.InputRoot.Value, builder.OutputRoot.Value);

            if (options.OpenBrowser)
            {
                TryOpenBrowser(url);
            }

            try
            {
                using var watcher = new WatchLoop(builder.InputRoot, options.WatchOutput ? builder.OutputRoot : null, options.DebounceMs, logger);
                await foreach (var changes in watcher.WaitAsync(combinedToken).ConfigureAwait(false))
                {
                    await RebuildAndSignalAsync(builder, broker, logger, changes, combinedToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on Ctrl+C / token cancellation; fall through to shutdown.
            }
            finally
            {
                ServeLoggingHelper.LogServerStopping(logger);

                // Abort tracked WebSockets up-front so the in-flight LiveReload handlers exit
                // promptly. Browsers don't always reply to the close handshake; relying on a
                // graceful close lets Ctrl+C hang forever.
                broker.AbortAll();

                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await app.StopAsync(stopCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 2-second shutdown budget elapsed; fall through to DisposeAsync.
                }

                await app.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            Console.CancelKeyPress -= consoleHandler;
        }
    }

    /// <summary>Runs one rebuild and signals connected browsers when it succeeds.</summary>
    /// <param name="builder">Builder to drive.</param>
    /// <param name="broker">LiveReload registry.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="changes">Changed paths in this debounce window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async task.</returns>
    private static async Task RebuildAndSignalAsync(DocBuilder builder, LiveReloadBroker broker, ILogger logger, HashSet<string> changes, CancellationToken cancellationToken)
    {
        ServeLoggingHelper.LogRebuildStart(logger, changes.Count);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await builder.BuildAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            ServeLoggingHelper.LogRebuildFailed(logger, ex);
            return;
        }

        stopwatch.Stop();
        var sent = await broker.ReloadAllAsync().ConfigureAwait(false);
        ServeLoggingHelper.LogRebuildComplete(logger, stopwatch.ElapsedMilliseconds, sent);
    }

    /// <summary>Best-effort cross-platform "open URL in default browser" — silently swallows any failure.</summary>
    /// <param name="url">URL to open.</param>
    private static void TryOpenBrowser(UrlPath url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            };
            Process.Start(psi);
        }
        catch (Win32Exception)
        {
            // No registered handler for the URL on this platform — give up.
        }
        catch (FileNotFoundException)
        {
            // Same root cause on macOS / Linux without `xdg-open` etc.
        }
    }
}
