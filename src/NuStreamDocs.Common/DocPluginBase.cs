// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Common;

/// <summary>
/// Convenience base class for <see cref="IDocPlugin"/> implementations
/// that only care about a subset of the lifecycle hooks. Each hook
/// returns <see cref="ValueTask.CompletedTask"/> by default; concrete
/// plugins override only the hook they implement.
/// </summary>
/// <remarks>
/// Wipes the per-plugin "swallow context, swallow CT, return
/// <see cref="ValueTask.CompletedTask"/>" boilerplate that was being
/// stamped out 30+ times across the plugin assemblies and showing up
/// in the duplication reports as 100% identical 12-line blocks.
/// </remarks>
public abstract class DocPluginBase : IDocPlugin
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public virtual ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }
}
