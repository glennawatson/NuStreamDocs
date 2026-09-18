// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>Token classifications used by the highlighter; mapped to short-form CSS class names by <see cref="TokenClassNames.Css"/>.</summary>
public enum TokenClass
{
    /// <summary>Plain source text with no specific classification.</summary>
    Text = 0,

    /// <summary>Whitespace characters.</summary>
    Whitespace = 1,

    /// <summary>Generic name.</summary>
    Name = 2,

    /// <summary>Function or method name.</summary>
    NameFunction = 3,

    /// <summary>Class / type name.</summary>
    NameClass = 4,

    /// <summary>Built-in identifier (true / false / null).</summary>
    NameBuiltin = 5,

    /// <summary>Attribute name.</summary>
    NameAttribute = 6,

    /// <summary>Language keyword.</summary>
    Keyword = 7,

    /// <summary>Constant keyword (true / false / null).</summary>
    KeywordConstant = 8,

    /// <summary>Declaration keyword (var / let / class / def / fn).</summary>
    KeywordDeclaration = 9,

    /// <summary>Type keyword (int / string / bool).</summary>
    KeywordType = 10,

    /// <summary>Language operator.</summary>
    Operator = 11,

    /// <summary>Punctuation characters.</summary>
    Punctuation = 12,

    /// <summary>String literal — generic.</summary>
    String = 13,

    /// <summary>Single-quoted string literal.</summary>
    StringSingle = 14,

    /// <summary>Double-quoted string literal.</summary>
    StringDouble = 15,

    /// <summary>Escape sequence inside a string.</summary>
    StringEscape = 16,

    /// <summary>Integer literal.</summary>
    NumberInteger = 17,

    /// <summary>Floating-point literal.</summary>
    NumberFloat = 18,

    /// <summary>Hexadecimal integer literal.</summary>
    NumberHex = 19,

    /// <summary>Single-line comment.</summary>
    CommentSingle = 20,

    /// <summary>Multi-line comment.</summary>
    CommentMulti = 21,

    /// <summary>Documentation comment.</summary>
    CommentSpecial = 22,

    /// <summary>Preprocessor directive.</summary>
    CommentPreproc = 23,

    /// <summary>Diff-format added line (<c>+ ...</c>); rendered as the<c>gi</c> CSS class.</summary>
    DiffAddedLine = 24,

    /// <summary>Diff-format removed line (<c>- ...</c>); rendered as the<c>gd</c> CSS class.</summary>
    DiffRemovedLine = 25,

    /// <summary>Diff-format file header (<c>--- a/file</c>, <c>+++ b/file</c>, <c>diff …</c>, <c>index …</c>); rendered as the<c>gh</c> CSS class.</summary>
    DiffFileHeader = 26,

    /// <summary>Diff-format hunk header (<c>@@ … @@</c>); rendered as the<c>gu</c> CSS class.</summary>
    DiffHunkHeader = 27,
}
