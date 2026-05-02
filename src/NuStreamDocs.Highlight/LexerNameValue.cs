// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>
/// The name / value pair of UTF-8 language id and lexer instance — the
/// shape callers use to register extra lexers with
/// <see cref="LexerRegistry.Build(LexerNameValue[])"/>.
/// </summary>
/// <param name="LanguageId">UTF-8 language id (lowercase by convention).</param>
/// <param name="Lexer">Lexer instance.</param>
public readonly record struct LexerNameValue(byte[] LanguageId, Lexer Lexer);
