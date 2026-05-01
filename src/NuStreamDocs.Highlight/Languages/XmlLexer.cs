// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>XML/HTML lexer modelled on Pygments' <c>HtmlLexer</c> shape.</summary>
/// <remarks>
/// Two-state machine: <c>root</c> walks document text and
/// recognises tag opens / comments / DOCTYPE / CDATA;
/// <c>tag</c> handles attribute names, equals, and quoted values
/// before popping back at <c>&gt;</c> or <c>/&gt;</c>.
/// CSS classes match Pygments — <c>nt</c> for tag names is folded
/// into <see cref="TokenClass.NameClass"/> (CSS class <c>nc</c>),
/// which Pygments themes also style; the surrounding angle brackets
/// land as punctuation.
/// </remarks>
public static partial class XmlLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the lexer with both states populated.</summary>
    /// <returns>Configured lexer.</returns>
    private static Lexer Build()
    {
        var states = new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] =
            [
                new(LanguageCommon.WhitespaceWithNewlines(), TokenClass.Whitespace, NextState: null) { FirstChars = LanguageCommon.WhitespaceWithNewlinesFirst },
                new(CommentRegex(), TokenClass.CommentMulti, NextState: null) { FirstChars = LanguageCommon.AngleOpenFirst },
                new(CDataRegex(), TokenClass.CommentSpecial, NextState: null) { FirstChars = LanguageCommon.AngleOpenFirst },
                new(DoctypeRegex(), TokenClass.CommentPreproc, NextState: null) { FirstChars = LanguageCommon.AngleOpenFirst },
                new(ProcessingInstructionRegex(), TokenClass.CommentPreproc, NextState: null) { FirstChars = LanguageCommon.AngleOpenFirst },
                new(LanguageCommon.EntityReference(), TokenClass.StringEscape, NextState: null) { FirstChars = LanguageCommon.EntityFirst },
                new(LanguageCommon.AngleOpen(), TokenClass.Punctuation, "tag") { FirstChars = LanguageCommon.AngleOpenFirst },
                new(LanguageCommon.AngleOpenSlash(), TokenClass.Punctuation, "tag") { FirstChars = LanguageCommon.AngleOpenFirst },
                new(TextRegex(), TokenClass.Text, NextState: null),
            ],

            ["tag"] = MarkupTagRules.Build(),
        }.ToFrozenDictionary(StringComparer.Ordinal);
        return new("xml", states);
    }

    [GeneratedRegex(@"\G<!--[\s\S]*?-->", RegexOptions.Compiled)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\G<!\[CDATA\[[\s\S]*?\]\]>", RegexOptions.Compiled)]
    private static partial Regex CDataRegex();

    [GeneratedRegex(@"\G<!DOCTYPE[\s\S]*?>", RegexOptions.Compiled)]
    private static partial Regex DoctypeRegex();

    [GeneratedRegex(@"\G<\?[\s\S]*?\?>", RegexOptions.Compiled)]
    private static partial Regex ProcessingInstructionRegex();

    [GeneratedRegex(@"\G[^<&]+", RegexOptions.Compiled)]
    private static partial Regex TextRegex();
}
