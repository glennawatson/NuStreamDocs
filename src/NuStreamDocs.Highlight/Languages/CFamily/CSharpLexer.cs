// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>C# lexer. Three states (root, block-accessor, arrow-accessor) so <c>field</c>/<c>value</c> highlight as keywords inside property accessors only.</summary>
public static class CSharpLexer
{
    /// <summary>State id for an accessor's <c>{...}</c> body. Push on <c>get/set/init</c> + <c>{</c>; <c>{</c> nests, <c>}</c> pops.</summary>
    private const int BlockAccessorStateId = 1;

    /// <summary>State id for an accessor's <c>=&gt;</c> arrow body. Push on <c>get/set/init</c> + <c>=&gt;</c>; <c>;</c> pops.</summary>
    private const int ArrowAccessorStateId = 2;

    /// <summary>Gets the singleton <see cref="Lexer"/> for C#.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the three-state C# lexer.</summary>
    /// <returns>Configured lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[][] states =
        [
            CSharpRules.Build(BlockAccessorStateId, ArrowAccessorStateId),
            CSharpRules.BuildBlockAccessorRules(BlockAccessorStateId),
            CSharpRules.BuildArrowAccessorRules()
        ];
        return new(states);
    }
}
