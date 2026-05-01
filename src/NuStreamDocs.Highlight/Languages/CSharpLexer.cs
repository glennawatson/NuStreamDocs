// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>C# lexer.</summary>
/// <remarks>
/// Modelled on Pygments' <c>CSharpLexer</c>; rules live in
/// <see cref="CSharpRules"/> so embedded-language scenarios
/// (Razor's <c>@code</c> blocks, future Blazor components) classify
/// the same tokens with the same Pygments-shape CSS classes.
/// <para>
/// Three states: <c>root</c>, <see cref="CSharpRules.BlockAccessorState"/>,
/// and <see cref="CSharpRules.ArrowAccessorState"/>. The two accessor
/// states recognise <c>field</c> and <c>value</c> as keywords so the
/// C# 13 backing-field syntax highlights correctly inside property
/// accessors — but those names stay as plain identifiers everywhere
/// else.
/// </para>
/// </remarks>
public static class CSharpLexer
{
    /// <summary>Gets the singleton <see cref="Lexer"/> for C#.</summary>
    public static Lexer Instance { get; } = new(
        "csharp",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] = CSharpRules.Build(),
            [CSharpRules.BlockAccessorState] = CSharpRules.BuildBlockAccessorRules(),
            [CSharpRules.ArrowAccessorState] = CSharpRules.BuildArrowAccessorRules(),
        }.ToFrozenDictionary(StringComparer.Ordinal));
}
