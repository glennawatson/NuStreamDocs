// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>Plugin that counts hook invocations for pipeline tests.</summary>
/// <remarks>
/// The hook bodies are called from <c>Parallel.ForEachAsync</c> workers,
/// so counters must be incremented atomically. <c>Interlocked.Increment(ref int)</c>
/// requires a <c>ref</c> to real storage, which property accessors cannot
/// expose — this is the exception to the C# 14 <c>field</c> rule.
/// </remarks>
internal sealed class CountingPlugin : IBuildConfigurePlugin, IPagePostRenderPlugin, IBuildFinalizePlugin
{
    /// <summary>Backing counter for <c>ConfigureHits</c>; touched via <c>Interlocked</c>.</summary>
    private int _configureHits;

    /// <summary>Backing counter for <c>PageHits</c>; touched via <c>Interlocked</c>.</summary>
    private int _pageHits;

    /// <summary>Backing counter for <c>FinalizeHits</c>; touched via <c>Interlocked</c>.</summary>
    private int _finalizeHits;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "counting"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <summary>Gets the number of times <c>ConfigureAsync</c> was invoked.</summary>
    public int ConfigureHits => Volatile.Read(ref _configureHits);

    /// <summary>Gets the number of times <c>PostRender</c> was invoked.</summary>
    public int PageHits => Volatile.Read(ref _pageHits);

    /// <summary>Gets the number of times <c>FinalizeAsync</c> was invoked.</summary>
    public int FinalizeHits => Volatile.Read(ref _finalizeHits);

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        Interlocked.Increment(ref _configureHits);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => true;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        Interlocked.Increment(ref _pageHits);
        context.Output.Write(context.Html);
    }

    /// <inheritdoc/>
    public ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        Interlocked.Increment(ref _finalizeHits);
        return ValueTask.CompletedTask;
    }
}
