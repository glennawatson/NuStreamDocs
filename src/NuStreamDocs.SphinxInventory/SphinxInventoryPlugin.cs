// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Autorefs;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SphinxInventory;

/// <summary>
/// Emits a Sphinx-compatible <c>objects.inv</c> file at finalise time
/// so external Sphinx sites can intersphinx-link into the build's
/// rendered output.
/// </summary>
/// <remarks>
/// Reads the same shared <see cref="AutorefsRegistry"/> the autorefs /
/// xrefs plugins write into, so any UID a NuStreamDocs page exposes is
/// reachable from a Sphinx site that pulls this <c>objects.inv</c>.
/// </remarks>
public sealed class SphinxInventoryPlugin : IDocPlugin
{
    /// <summary>Configured options.</summary>
    private readonly SphinxInventoryOptions _options;

    /// <summary>Initializes a new instance of the <see cref="SphinxInventoryPlugin"/> class with default options and a fresh registry.</summary>
    public SphinxInventoryPlugin()
        : this(new(), SphinxInventoryOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SphinxInventoryPlugin"/> class with default options.</summary>
    /// <param name="registry">Shared autorefs registry.</param>
    public SphinxInventoryPlugin(AutorefsRegistry registry)
        : this(registry, SphinxInventoryOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SphinxInventoryPlugin"/> class.</summary>
    /// <param name="registry">Shared autorefs registry.</param>
    /// <param name="options">Plugin options.</param>
    public SphinxInventoryPlugin(AutorefsRegistry registry, SphinxInventoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);
        Registry = registry;
        _options = options;
    }

    /// <summary>Gets the shared registry the plugin reads from at finalise time.</summary>
    public AutorefsRegistry Registry { get; }

    /// <inheritdoc/>
    public string Name => "sphinx-inventory";

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
    public ValueTask OnFinaliseAsync(PluginFinaliseContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var outputPath = Path.Combine(context.OutputRoot, _options.OutputFileName);
        SphinxInventoryWriter.Write(outputPath, _options, Registry.Snapshot());
        return ValueTask.CompletedTask;
    }
}
