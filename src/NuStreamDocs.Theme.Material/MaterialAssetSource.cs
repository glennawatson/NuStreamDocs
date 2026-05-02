// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material;

/// <summary>
/// Where the rendered pages should source the Material CSS / JS bundle
/// from.
/// </summary>
public enum MaterialAssetSource
{
    /// <summary>
    /// Bundle the assets into the output directory.
    /// </summary>
    /// <remarks>
    /// The plugin writes <c>{output}/assets/stylesheets/material.min.css</c>
    /// (and friends) at finalization time, and the page template
    /// references them with a relative path. The build is fully self
    /// contained — no external network access at view time.
    /// </remarks>
    Embedded = 0,

    /// <summary>
    /// Skip writing local copies and reference the assets from a CDN
    /// instead.
    /// </summary>
    /// <remarks>
    /// The page template's <c>{{asset_root}}</c> substitution becomes
    /// the configured CDN root. Useful for environments that prefer
    /// edge caching, lighter deploy artefacts, or shared assets across
    /// many sites — at the cost of a runtime dependency on a third
    /// party.
    /// </remarks>
    Cdn,
}
