// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Represents a rule used by the lexer to match a specific pattern in the input
/// and assign a token classification. It also defines state transitions based
/// on the parsing process.
/// </summary>
/// <param name="Match">The matcher used to identify the pattern in the input.</param>
/// <param name="TokenClass">The classification assigned to the matched token.</param>
/// <param name="NextState">The state to transition to after a successful match (see <see cref="NoStateChange"/> and <see cref="PopState"/>).</param>
public sealed record LexerRule(LexerRuleMatcher Match, TokenClass TokenClass, int NextState)
{
    /// <summary>Sentinel for <see cref="NextState"/> — leaves the state stack unchanged.</summary>
    internal const int NoStateChange = -1;

    /// <summary>Sentinel for <see cref="NextState"/> — pops one frame off the state stack.</summary>
    internal const int PopState = -2;

    /// <summary>Gets the optional first-byte dispatch set; when non-null, the lexer skips this rule unless the cursor byte is in the set.</summary>
    public SearchValues<byte>? FirstBytes { get; init; }

    /// <summary>Gets a value indicating whether the rule may only match at the start of a logical line (cursor is at <c>pos == 0</c> or the previous byte is <c>\n</c> / <c>\r</c>).</summary>
    public bool RequiresLineStart { get; init; }
}
