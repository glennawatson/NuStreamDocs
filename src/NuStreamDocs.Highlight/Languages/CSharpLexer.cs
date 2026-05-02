// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>C# lexer.</summary>
/// <remarks>
/// Modeled on Pygments' <c>CSharpLexer</c>; rules live in
/// <see cref="CSharpRules"/> so embedded-language scenarios
/// (Razor's <c>@code</c> blocks, future Blazor components) classify
/// the same tokens with the same Pygments-shape CSS classes.
/// <para>
/// Three states: root, block-accessor, arrow-accessor. The two accessor
/// states recognize <c>field</c> and <c>value</c> as keywords so the
/// C# 13 backing-field syntax highlights correctly inside property
/// accessors — but those names stay as plain identifiers everywhere
/// else.
/// </para>
/// </remarks>
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
        var states = new LexerRule[3][];
        states[Lexer.RootStateId] = CSharpRules.Build(BlockAccessorStateId, ArrowAccessorStateId);
        states[BlockAccessorStateId] = CSharpRules.BuildBlockAccessorRules(BlockAccessorStateId);
        states[ArrowAccessorStateId] = CSharpRules.BuildArrowAccessorRules();
        return new(states);
    }
}
