// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.MarkdownExtensions.CriticMarkup;

/// <summary>The five CriticMarkup span shapes plus a sentinel for "no match".</summary>
internal enum CriticMarker
{
    /// <summary>No match.</summary>
    None = 0,

    /// <summary><c>{++ … ++}</c> — insertion.</summary>
    Insert = 1,

    /// <summary><c>{-- … --}</c> — deletion.</summary>
    Delete = 2,

    /// <summary><c>{~~old~&gt;new~~}</c> — substitution.</summary>
    Substitute = 3,

    /// <summary><c>{== … ==}</c> — highlight.</summary>
    Highlight = 4,

    /// <summary><c>{&gt;&gt; … &lt;&lt;}</c> — comment.</summary>
    Comment = 5,
}
