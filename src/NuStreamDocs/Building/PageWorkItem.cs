// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Building;

/// <summary>
/// One unit of work flowing through the build pipeline.
/// </summary>
/// <remarks>
/// Streams from <c>PageDiscovery.EnumerateAsync</c> through the
/// per-page parse + emit + write stages. Records the absolute source
/// path and the relative path the renderer keys into nav, autorefs and
/// search; never holds the file bytes — those are loaded inside the
/// per-page rental so memory stays proportional to the active worker
/// count, not the corpus size.
/// </remarks>
/// <param name="AbsolutePath">Absolute on-disk path to the source markdown.</param>
/// <param name="RelativePath">Path relative to the input root, forward-slashed.</param>
/// <param name="Flags">Frontmatter-derived flags (<c>draft</c>, <c>not_in_nav</c>) read once during discovery.</param>
public readonly record struct PageWorkItem(
    string AbsolutePath,
    string RelativePath,
    PageFlags Flags);
