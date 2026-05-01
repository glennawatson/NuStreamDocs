// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight;

/// <summary>
/// One rule in a Pygments-style state-machine lexer.
/// </summary>
/// <remarks>
/// Each rule pairs a regex with a token classification; matching the
/// regex emits a token of <see cref="TokenClass"/> and (optionally)
/// transitions the lexer to <see cref="NextState"/>. The regex is
/// expected to be anchored at the cursor — built-in lexers compile
/// every pattern via <c>[GeneratedRegex]</c> so the DFA is built at
/// the source-generator step.
/// <para>
/// When <see cref="FirstChars"/> is non-null, the lexer's per-position
/// dispatch checks the cursor character against the set before invoking
/// the regex; rules whose first byte can't possibly match are skipped
/// entirely. Rules without a first-char hint always run the regex.
/// </para>
/// </remarks>
/// <param name="Pattern">Regex matched against the cursor; should anchor at <c>\G</c>.</param>
/// <param name="TokenClass">Classification emitted on match.</param>
/// <param name="NextState">State to transition into; null leaves the state unchanged. Use <see cref="StatePop"/> to pop one frame off the stack.</param>
public sealed record LexerRule(Regex Pattern, TokenClass TokenClass, string? NextState)
{
    /// <summary>Gets the reserved sentinel used in <see cref="NextState"/> to pop one frame.</summary>
    public static string StatePop => "#pop";

    /// <summary>Gets the optional first-character dispatch set; when non-null, the lexer skips this rule unless the cursor character is in the set.</summary>
    /// <remarks>
    /// Use <see cref="SearchValues.Create(System.ReadOnlySpan{char})"/> to build the set
    /// once at lexer-construction time. Leave null when the pattern can match a wide
    /// or unbounded character set (e.g. <c>.</c>, <c>\w</c>, complex alternations).
    /// </remarks>
    public SearchValues<char>? FirstChars { get; init; }
}
