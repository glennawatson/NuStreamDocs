// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>Inline directive classification applied to a single rendered code line.</summary>
internal enum LineAnnotationKind
{
    /// <summary>Standard code line without diff or focus styling.</summary>
    None = 0,

    /// <summary>Inserted diff line (<c>[!code ++]</c>).</summary>
    Added = 1,

    /// <summary>Removed diff line (<c>[!code --]</c>).</summary>
    Deleted = 2,

    /// <summary>Focused line (<c>[!code focus]</c>); non-focused sibling lines dim until hover.</summary>
    Focused = 3,
}
