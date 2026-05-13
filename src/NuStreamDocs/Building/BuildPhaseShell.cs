// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Building;

/// <summary>Bundle of shared build-wide state threaded through fire helpers.</summary>
/// <param name="InputRoot">Absolute input root.</param>
/// <param name="OutputRoot">Absolute output root.</param>
/// <param name="Options">Pipeline options.</param>
/// <param name="PluginTiming">Per-plugin time accumulator.</param>
/// <param name="Log">Logger.</param>
internal readonly record struct BuildPhaseShell(
    DirectoryPath InputRoot,
    DirectoryPath OutputRoot,
    BuildPipelineOptions Options,
    PluginTimingTable PluginTiming,
    ILogger Log);
