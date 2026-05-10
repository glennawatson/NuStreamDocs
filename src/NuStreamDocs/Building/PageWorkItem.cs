// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Building;

/// <summary>One unit of work flowing through the build pipeline.</summary>
/// <param name="AbsolutePath">Absolute on-disk path to the source markdown; empty for in-memory synthetic pages (see <see cref="InMemorySource"/>).</param>
/// <param name="RelativePath">Path relative to the input root, forward-slashed.</param>
/// <param name="Flags">Frontmatter-derived flags (<c>draft</c>, <c>not_in_nav</c>) read once during discovery.</param>
public readonly record struct PageWorkItem(
    FilePath AbsolutePath,
    FilePath RelativePath,
    PageFlags Flags)
{
    /// <summary>Gets the in-memory UTF-8 markdown source when this work item came from a discovery-phase synthetic-page registration; <c>null</c> for disk-loaded pages.</summary>
    public byte[]? InMemorySource { get; init; }
}
