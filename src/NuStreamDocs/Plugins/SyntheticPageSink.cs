// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Thread-safe collector for <see cref="SyntheticPage"/> entries that discovery-phase plugins
/// register instead of writing intermediate <c>.md</c> files into the source folder. After
/// the discover phase completes the build pipeline drains the sink and yields the entries
/// alongside disk-loaded pages. Plugins that produce a small known set of pages add eagerly;
/// plugins that emit many pages register a stream so the pipeline pulls one page at a time
/// and lets each render before reading the next.
/// </summary>
public sealed class SyntheticPageSink
{
    /// <summary>Backing collection. <see cref="ConcurrentBag{T}"/> because plugin discover hooks may run concurrently in future and registration order is irrelevant.</summary>
    private readonly ConcurrentBag<SyntheticPage> _pages = [];

    /// <summary>Registered async streams; drained after the eager bag.</summary>
    private readonly ConcurrentBag<IAsyncEnumerable<SyntheticPage>> _streams = [];

    /// <summary>Gets the count of eagerly-added synthetic pages.</summary>
    public int Count => _pages.Count;

    /// <summary>Gets the number of registered streams the pipeline will drain after the eager bag.</summary>
    public int StreamCount => _streams.Count;

    /// <summary>Adds <paramref name="page"/> to the sink.</summary>
    /// <param name="page">The synthetic page to register.</param>
    public void Add(in SyntheticPage page) => _pages.Add(page);

    /// <summary>Adds a synthetic page with the given relative path and markdown bytes.</summary>
    /// <param name="relativePath">Forward-slashed path relative to the input root.</param>
    /// <param name="markdownBytes">UTF-8 markdown source.</param>
    public void Add(in Common.FilePath relativePath, byte[] markdownBytes) =>
        _pages.Add(new(relativePath, markdownBytes));

    /// <summary>Registers an async stream the pipeline will drain after the eager bag is consumed.</summary>
    /// <param name="stream">A lazy stream of pages; pulled one at a time so the producer can hold peak memory low.</param>
    public void RegisterStream(IAsyncEnumerable<SyntheticPage> stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _streams.Add(stream);
    }

    /// <summary>Returns the registered eagerly-added pages as an array snapshot.</summary>
    /// <returns>Snapshot array; safe to enumerate without further locking.</returns>
    public SyntheticPage[] Snapshot() => [.. _pages];

    /// <summary>Drains the sink — eager pages first, then each registered stream in turn.</summary>
    /// <param name="cancellationToken">Cancellation token observed between pages.</param>
    /// <returns>An async stream of every page registered with the sink.</returns>
    public async IAsyncEnumerable<SyntheticPage> DrainAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var page in _pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return page;
        }

        foreach (var stream in _streams)
        {
            await foreach (var page in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return page;
            }
        }
    }
}
