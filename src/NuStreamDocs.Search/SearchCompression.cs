// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search;

/// <summary>Index-on-disk compression knob honored by <see cref="SearchPlugin"/>.</summary>
public enum SearchCompression
{
    /// <summary>No compression — emit the index as plain UTF-8 JSON. Largest file, fastest write, easiest to inspect.</summary>
    None,

    /// <summary>Emit the index as plain JSON plus a sibling <c>.gz</c> the static-server (or CDN) can serve via <c>Content-Encoding: gzip</c>.</summary>
    Default,

    /// <summary>Emit a sibling <c>.br</c> alongside the plain JSON for CDNs that prefer Brotli; smallest payload, slower build.</summary>
    Smallest
}
