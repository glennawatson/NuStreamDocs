// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Building;

/// <summary>
/// Build-pipeline flags lifted from a page's frontmatter.
/// </summary>
[Flags]
public enum PageFlags
{
    /// <summary>No flags set — page builds normally and is included in nav.</summary>
    None = 0,

    /// <summary>Page declares <c>draft: true</c>; build pipeline skips it unless <c>IncludeDrafts</c> is on.</summary>
    Draft = 1 << 0,

    /// <summary>Page declares <c>not_in_nav: true</c> (or <c>nav_exclude</c>); built but excluded from the nav tree.</summary>
    NotInNav = 1 << 1,
}
