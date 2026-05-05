// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>Default ordering applied when no override file is present.</summary>
public enum NavSortBy
{
    /// <summary>Order pages by file name, case-insensitive.</summary>
    FileName,

    /// <summary>Order pages by their first H1 heading, case-insensitive.</summary>
    Title,

    /// <summary>Preserve filesystem enumeration order.</summary>
    None
}
