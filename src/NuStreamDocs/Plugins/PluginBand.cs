// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Coarse ordering band a plugin bids for within a phase.
/// </summary>
/// <remarks>
/// Plugins declare a band per implemented phase via
/// <see cref="PluginPriority"/>. The engine sorts each per-phase array
/// once at build start by ascending <see cref="PluginPriority.Band"/>,
/// then by <see cref="PluginPriority.Tiebreak"/>. Use the predefined
/// bands so most plugins compose without coordinating numeric values;
/// reach for <see cref="PluginPriority.Tiebreak"/> only when two
/// plugins share a band and one must precede the other.
/// </remarks>
public enum PluginBand
{
    /// <summary>Runs before everything else in the phase.</summary>
    Earliest = -100,

    /// <summary>Runs before the default crowd.</summary>
    Early = -50,

    /// <summary>Default band — most plugins should declare this.</summary>
    Normal = 0,

    /// <summary>Runs after the default crowd.</summary>
    Late = 50,

    /// <summary>Runs after everything else in the phase.</summary>
    Latest = 100,
}
