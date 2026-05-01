// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>
/// Pygments-compatible token classifications.
/// </summary>
/// <remarks>
/// The string forms exposed by <see cref="TokenClassNames.Css"/> match
/// Pygments' short-form CSS class taxonomy exactly so existing
/// mkdocs-material stylesheets — and any other Pygments theme — light
/// up against our output without re-skinning.
/// </remarks>
public enum TokenClass
{
    /// <summary>Plain source text with no specific classification.</summary>
    Text,

    /// <summary>Whitespace.</summary>
    Whitespace,

    /// <summary>Generic name.</summary>
    Name,

    /// <summary>Function or method name.</summary>
    NameFunction,

    /// <summary>Class / type name.</summary>
    NameClass,

    /// <summary>Built-in identifier (true / false / null).</summary>
    NameBuiltin,

    /// <summary>Attribute name.</summary>
    NameAttribute,

    /// <summary>Keyword.</summary>
    Keyword,

    /// <summary>Constant keyword (true / false / null).</summary>
    KeywordConstant,

    /// <summary>Declaration keyword (var / let / class / def / fn).</summary>
    KeywordDeclaration,

    /// <summary>Type keyword (int / string / bool).</summary>
    KeywordType,

    /// <summary>Operator.</summary>
    Operator,

    /// <summary>Punctuation.</summary>
    Punctuation,

    /// <summary>String literal — generic.</summary>
    String,

    /// <summary>Single-quoted string literal.</summary>
    StringSingle,

    /// <summary>Double-quoted string literal.</summary>
    StringDouble,

    /// <summary>Escape sequence inside a string.</summary>
    StringEscape,

    /// <summary>Integer literal.</summary>
    NumberInteger,

    /// <summary>Floating-point literal.</summary>
    NumberFloat,

    /// <summary>Hexadecimal integer literal.</summary>
    NumberHex,

    /// <summary>Single-line comment.</summary>
    CommentSingle,

    /// <summary>Multi-line comment.</summary>
    CommentMulti,

    /// <summary>Documentation comment.</summary>
    CommentSpecial,

    /// <summary>Preprocessor directive.</summary>
    CommentPreproc,
}
