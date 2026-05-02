// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>
/// Span matcher: returns the number of UTF-8 bytes matched at the
/// lexer cursor, or <c>0</c> on miss.
/// </summary>
/// <param name="slice">UTF-8 byte span starting at the lexer cursor.</param>
/// <returns>Length matched on success; <c>0</c> on no match.</returns>
public delegate int LexerRuleMatcher(ReadOnlySpan<byte> slice);
