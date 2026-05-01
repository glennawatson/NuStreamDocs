// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight;

/// <summary>
/// One rule in a Pygments-style state-machine lexer.
/// </summary>
/// <remarks>
/// Each rule pairs a span matcher with a token classification; on a
/// positive match the lexer emits a token of <see cref="TokenClass"/>
/// and (optionally) transitions into <see cref="NextState"/>.
/// <para>
/// When <see cref="FirstChars"/> is non-null, the lexer's per-position
/// dispatch checks the cursor character against the set before invoking
/// the matcher; rules whose first character can't possibly match are
/// skipped entirely.
/// </para>
/// </remarks>
/// <param name="Match">Matcher invoked at the cursor — returns the matched length, or <c>0</c> on miss.</param>
/// <param name="TokenClass">Classification emitted on match.</param>
/// <param name="NextState">State to transition into; null leaves the state unchanged. Use <see cref="StatePop"/> to pop one frame off the stack.</param>
public sealed record LexerRule(LexerRule.Matcher Match, TokenClass TokenClass, string? NextState)
{
    /// <summary>Span matcher: returns the number of characters matched at the cursor, or <c>0</c> on miss.</summary>
    /// <param name="slice">Span starting at the lexer cursor.</param>
    /// <returns>Length matched on success; <c>0</c> on no match.</returns>
    public delegate int Matcher(ReadOnlySpan<char> slice);

    /// <summary>Gets the reserved sentinel used in <see cref="NextState"/> to pop one frame.</summary>
    public static string StatePop => "#pop";

    /// <summary>Gets the optional first-character dispatch set; when non-null, the lexer skips this rule unless the cursor character is in the set.</summary>
    public SearchValues<char>? FirstChars { get; init; }

    /// <summary>Gets a value indicating whether the rule may only match at the start of a logical line (cursor is at <c>pos == 0</c> or the previous character is <c>\n</c> / <c>\r</c>).</summary>
    public bool RequiresLineStart { get; init; }
}
