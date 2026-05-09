// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight;

/// <summary>One rule the lexer evaluates: pattern matcher, token classification, and state transition.</summary>
/// <param name="Match">Matcher used to identify the pattern.</param>
/// <param name="TokenClass">Classification assigned on a successful match.</param>
/// <param name="NextState">State to transition to after a match (see <see cref="NoStateChange"/> / <see cref="PopState"/>).</param>
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
