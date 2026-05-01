// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Razor / cshtml lexer.</summary>
/// <remarks>
/// Pragmatic shape: HTML-style root state plus a <c>csharp</c> state
/// that reuses <see cref="CSharpRules.Build"/>. Razor entry tokens
/// (<c>@code</c>, <c>@{</c>, <c>@if</c>, <c>@foreach</c>, …) push the
/// C# state; a closing <c>}</c> pops back to the markup. Inline
/// <c>@identifier</c> expressions stay in markup mode and classify
/// as <see cref="TokenClass.Name"/>; that's enough for the rxui
/// corpus's Razor blocks, which are mostly HTML with a few inline
/// directives.
/// </remarks>
public static partial class RazorLexer
{
    /// <summary>Name of the embedded C# state in the lexer's state map.</summary>
    private const string CSharpStateName = "csharp";

    /// <summary>Pattern alternation for Razor block-introducing directives that switch into C# state.</summary>
    private const string BlockDirectives =
        "code|functions|inherits|model|page|using|namespace|attribute|implements|" +
        "inject|layout|preservewhitespace|removetag|section|service|typeparam";

    /// <summary>Pattern alternation for inline Razor control-flow keywords.</summary>
    private const string ControlDirectives =
        "if|else|for|foreach|while|do|switch|case|default|return|try|catch|finally|" +
        "lock|using|await|async";

    /// <summary>First-char set for the C# closing brace popper.</summary>
    private static readonly SearchValues<char> CloseBraceFirst = SearchValues.Create("}");

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        "razor",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            // Markup state — HTML + Razor entry tokens.
            [Lexer.RootState] =
            [
                new(LanguageCommon.WhitespaceWithNewlines(), TokenClass.Whitespace, NextState: null) { FirstChars = LanguageCommon.WhitespaceWithNewlinesFirst },
                new(CommentRegex(), TokenClass.CommentMulti, NextState: null) { FirstChars = LanguageCommon.AtFirst },
                new(EscapedAtRegex(), TokenClass.Text, NextState: null) { FirstChars = LanguageCommon.AtFirst },
                new(BlockDirectiveRegex(), TokenClass.KeywordDeclaration, CSharpStateName) { FirstChars = LanguageCommon.AtFirst },
                new(ControlDirectiveRegex(), TokenClass.Keyword, CSharpStateName) { FirstChars = LanguageCommon.AtFirst },
                new(BraceBlockOpenRegex(), TokenClass.Punctuation, CSharpStateName) { FirstChars = LanguageCommon.AtFirst },
                new(InlineExpressionRegex(), TokenClass.Name, NextState: null) { FirstChars = LanguageCommon.AtFirst },
                new(LanguageCommon.EntityReference(), TokenClass.StringEscape, NextState: null) { FirstChars = LanguageCommon.EntityFirst },
                new(LanguageCommon.AngleOpen(), TokenClass.Punctuation, "tag") { FirstChars = LanguageCommon.AngleOpenFirst },
                new(LanguageCommon.AngleOpenSlash(), TokenClass.Punctuation, "tag") { FirstChars = LanguageCommon.AngleOpenFirst },
                new(TextRegex(), TokenClass.Text, NextState: null),
            ],

            ["tag"] = MarkupTagRules.Build(),

            // C# state — reuse the shared rule list and add a closing-
            // brace rule that pops back to markup. The closing brace
            // takes precedence over the matching punctuation rule in
            // CSharpRules because it's listed first.
            [CSharpStateName] = BuildCsharpStateRules(),
        }.ToFrozenDictionary(StringComparer.Ordinal));

    /// <summary>Builds the C# state's rule list — a closing-brace popper plus the shared C# rules.</summary>
    /// <returns>Rule list.</returns>
    private static LexerRule[] BuildCsharpStateRules()
    {
        var shared = CSharpRules.Build();
        var combined = new LexerRule[shared.Length + 1];
        combined[0] = new(CsharpClosingBraceRegex(), TokenClass.Punctuation, LexerRule.StatePop) { FirstChars = CloseBraceFirst };
        Array.Copy(shared, 0, combined, 1, shared.Length);
        return combined;
    }

    [GeneratedRegex(@"\G@\*[\s\S]*?\*@", RegexOptions.Compiled)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\G@@", RegexOptions.Compiled)]
    private static partial Regex EscapedAtRegex();

    [GeneratedRegex(@"\G@(?:" + BlockDirectives + @")\b", RegexOptions.Compiled)]
    private static partial Regex BlockDirectiveRegex();

    [GeneratedRegex(@"\G@(?:" + ControlDirectives + @")\b", RegexOptions.Compiled)]
    private static partial Regex ControlDirectiveRegex();

    [GeneratedRegex(@"\G@\{", RegexOptions.Compiled)]
    private static partial Regex BraceBlockOpenRegex();

    [GeneratedRegex(@"\G@[A-Za-z_][A-Za-z0-9_.]*", RegexOptions.Compiled)]
    private static partial Regex InlineExpressionRegex();

    [GeneratedRegex(@"\G[^<&@]+", RegexOptions.Compiled)]
    private static partial Regex TextRegex();

    [GeneratedRegex(@"\G\}", RegexOptions.Compiled)]
    private static partial Regex CsharpClosingBraceRegex();
}
