// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuStreamDocs.Caching;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Building;

/// <summary>Bundle of per-page shared state threaded through helper methods.</summary>
/// <param name="Phases">Per-phase plugin arrays.</param>
/// <param name="Previous">Previous-build manifest.</param>
/// <param name="PluginTiming">Per-plugin time accumulator.</param>
/// <param name="Buffered">Queue receiving rentals when cross-page resolution is needed.</param>
/// <param name="CrossPageMarkerNeedles">Snapshot of marker byte sequences registered during configure; pages whose HTML contains none of them skip the cross-page buffer hold.</param>
internal readonly record struct PerPageDispatch(
    PluginPhases Phases,
    BuildManifest Previous,
    PluginTimingTable PluginTiming,
    ConcurrentQueue<BufferedPage> Buffered,
    byte[][] CrossPageMarkerNeedles);
