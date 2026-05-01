// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>Plugin that counts hook invocations for pipeline tests.</summary>
/// <remarks>
/// The hook bodies are called from <c>Parallel.ForEachAsync</c> workers,
/// so counters must be incremented atomically. <c>Interlocked.Increment(ref int)</c>
/// requires a <c>ref</c> to real storage, which property accessors cannot
/// expose — this is the exception to the C# 14 <c>field</c> rule.
/// </remarks>
internal sealed class CountingPlugin : IDocPlugin
{
    /// <summary>Backing counter for <c>ConfigureHits</c>; touched via <c>Interlocked</c>.</summary>
    private int _configureHits;

    /// <summary>Backing counter for <c>PageHits</c>; touched via <c>Interlocked</c>.</summary>
    private int _pageHits;

    /// <summary>Backing counter for <c>FinaliseHits</c>; touched via <c>Interlocked</c>.</summary>
    private int _finaliseHits;

    /// <inheritdoc/>
    public string Name => "counting";

    /// <summary>Gets the number of times <c>OnConfigureAsync</c> was invoked.</summary>
    public int ConfigureHits => Volatile.Read(ref _configureHits);

    /// <summary>Gets the number of times <c>OnRenderPageAsync</c> was invoked.</summary>
    public int PageHits => Volatile.Read(ref _pageHits);

    /// <summary>Gets the number of times <c>OnFinaliseAsync</c> was invoked.</summary>
    public int FinaliseHits => Volatile.Read(ref _finaliseHits);

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        Interlocked.Increment(ref _configureHits);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        Interlocked.Increment(ref _pageHits);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinaliseAsync(PluginFinaliseContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        Interlocked.Increment(ref _finaliseHits);
        return ValueTask.CompletedTask;
    }
}
