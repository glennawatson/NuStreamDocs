// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Feed;

/// <summary>Feed format selectors.</summary>
[Flags]
public enum FeedFormats
{
    /// <summary>No feed; the plugin is a no-op.</summary>
    None = 0,

    /// <summary>Emit an RSS 2.0 feed.</summary>
    Rss = 1,

    /// <summary>Emit an Atom 1.0 feed.</summary>
    Atom = 2,

    /// <summary>Emit both RSS and Atom feeds.</summary>
    Both = Rss | Atom
}
