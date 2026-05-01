// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Channels;
using NuStreamDocs.Serve.Logging;

namespace NuStreamDocs.Serve;

/// <summary>
/// Owns the <see cref="FileSystemWatcher"/> + debounce window. Coalesces
/// bursts of file-system events into a single rebuild trigger.
/// </summary>
/// <remarks>
/// Editors save in a flurry — atomic-rename, format-on-save, multiple
/// dirty buffers. We push every event onto an unbounded channel, then
/// wait <c>DebounceMs</c> after the last one before yielding a tick to
/// the caller. Callers run their rebuild then loop back for the next
/// debounced batch.
/// </remarks>
internal sealed class WatchLoop : IDisposable
{
    /// <summary>Output directory whose events we ignore (so the build doesn't trigger itself).</summary>
    private readonly string? _ignoreRoot;

    /// <summary>Debounce window in milliseconds.</summary>
    private readonly int _debounceMs;

    /// <summary>Logger for watch events.</summary>
    private readonly ILogger _logger;

    /// <summary>Unbounded channel of raw file-system events.</summary>
    private readonly Channel<string> _events;

    /// <summary>The underlying watcher; lifetime matches this loop.</summary>
    private readonly FileSystemWatcher _watcher;

    /// <summary>Initializes a new instance of the <see cref="WatchLoop"/> class and starts watching <paramref name="inputRoot"/>.</summary>
    /// <param name="inputRoot">Absolute or relative input directory to watch.</param>
    /// <param name="ignoreRoot">Output directory whose events are filtered out; <c>null</c> to disable filtering.</param>
    /// <param name="debounceMs">Debounce window in milliseconds.</param>
    /// <param name="logger">Logger.</param>
    public WatchLoop(string inputRoot, string? ignoreRoot, int debounceMs, ILogger logger)
    {
        var fullInput = Path.GetFullPath(inputRoot);
        _ignoreRoot = ignoreRoot is null ? null : Path.GetFullPath(ignoreRoot);
        _debounceMs = debounceMs;
        _logger = logger;
        _events = Channel.CreateUnbounded<string>(new()
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _watcher = new(fullInput)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnEvent;
        _watcher.Created += OnEvent;
        _watcher.Deleted += OnEvent;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;
    }

    /// <summary>Streams debounced rebuild signals; each yielded element carries the unique paths that changed in the window.</summary>
    /// <param name="cancellationToken">Cancellation token; cancellation gracefully ends the stream.</param>
    /// <returns>Async stream of debounced change-set tickets.</returns>
    public async IAsyncEnumerable<HashSet<string>> WaitAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reader = _events.Reader;
        while (!cancellationToken.IsCancellationRequested)
        {
            string first;
            try
            {
                first = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            var batch = new HashSet<string>(StringComparer.Ordinal) { first };
            await DrainDebounceWindowAsync(reader, batch, cancellationToken).ConfigureAwait(false);
            yield return batch;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnEvent;
        _watcher.Created -= OnEvent;
        _watcher.Deleted -= OnEvent;
        _watcher.Renamed -= OnRenamed;
        _watcher.Error -= OnError;
        _watcher.Dispose();
        _events.Writer.TryComplete();
    }

    /// <summary>Drains the channel for <c>_debounceMs</c> after the last event, accumulating into <paramref name="batch"/>.</summary>
    /// <param name="reader">Channel reader.</param>
    /// <param name="batch">Accumulator that the caller already seeded with the first event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async task that completes when the debounce window expires.</returns>
    private async Task DrainDebounceWindowAsync(ChannelReader<string> reader, HashSet<string> batch, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_debounceMs);
            try
            {
                var next = await reader.ReadAsync(deadline.Token).ConfigureAwait(false);
                batch.Add(next);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Returns true when <paramref name="path"/> falls inside the ignored output root.</summary>
    /// <param name="path">Absolute path from the watcher.</param>
    /// <returns>True when the event should be dropped.</returns>
    private bool ShouldIgnore(string path)
    {
        if (_ignoreRoot is null)
        {
            return false;
        }

        return path.StartsWith(_ignoreRoot, StringComparison.Ordinal);
    }

    /// <summary>FileSystemWatcher event handler for Changed/Created/Deleted.</summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">Event args.</param>
    private void OnEvent(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath))
        {
            return;
        }

        ServeLoggingHelper.LogWatchEvent(_logger, e.ChangeType, e.FullPath);
        _events.Writer.TryWrite(e.FullPath);
    }

    /// <summary>FileSystemWatcher event handler for Renamed.</summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">Event args.</param>
    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (ShouldIgnore(e.FullPath))
        {
            return;
        }

        ServeLoggingHelper.LogWatchEvent(_logger, e.ChangeType, e.FullPath);
        _events.Writer.TryWrite(e.FullPath);
    }

    /// <summary>FileSystemWatcher error handler — buffer overflows and similar.</summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">Error args.</param>
    private void OnError(object sender, ErrorEventArgs e)
    {
        ServeLoggingHelper.LogWatchError(_logger, e.GetException());
    }
}
