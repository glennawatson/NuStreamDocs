// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material3;

/// <summary>
/// Where the rendered pages should source the Material 3 CSS / JS
/// bundle from.
/// </summary>
public enum Material3AssetSource
{
    /// <summary>Bundle the assets into the output directory.</summary>
    Embedded = 0,

    /// <summary>Reference the assets from a CDN; skip the local write.</summary>
    Cdn
}
