// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Coarse ordering band a plugin bids for within a phase. The engine sorts per-phase arrays by
/// ascending <see cref="PluginPriority.Band"/> then <see cref="PluginPriority.Tiebreak"/>.
/// </summary>
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
    Latest = 100
}
