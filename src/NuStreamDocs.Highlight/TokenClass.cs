// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>Token classifications used by the highlighter.</summary>
/// <remarks>
/// The string forms exposed by <see cref="TokenClassNames.Css"/> use the
/// established short-form CSS class taxonomy (<c>k</c>, <c>kd</c>,
/// <c>kt</c>, <c>s</c>, <c>n</c>, <c>c1</c>, …) so existing
/// mkdocs-material stylesheets light up against our output without
/// re-skinning.
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

    /// <summary>Diff-format added line (<c>+ ...</c>); rendered as the<c>gi</c> CSS class.</summary>
    DiffAddedLine,

    /// <summary>Diff-format removed line (<c>- ...</c>); rendered as the<c>gd</c> CSS class.</summary>
    DiffRemovedLine,

    /// <summary>Diff-format file header (<c>--- a/file</c>, <c>+++ b/file</c>, <c>diff …</c>, <c>index …</c>); rendered as the<c>gh</c> CSS class.</summary>
    DiffFileHeader,

    /// <summary>Diff-format hunk header (<c>@@ … @@</c>); rendered as the<c>gu</c> CSS class.</summary>
    DiffHunkHeader
}
