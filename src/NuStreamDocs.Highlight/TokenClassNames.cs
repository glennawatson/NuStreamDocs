// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>Pygments-compatible CSS class names for each <see cref="TokenClass"/>.</summary>
/// <remarks>
/// The bytes returned from <see cref="Css"/> match Pygments' short-form
/// taxonomy: <c>k</c>, <c>kc</c>, <c>kt</c>, <c>kd</c>, <c>s</c>,
/// <c>s1</c>, <c>s2</c>, <c>se</c>, <c>n</c>, <c>nf</c>, <c>nc</c>,
/// <c>nb</c>, <c>na</c>, <c>o</c>, <c>p</c>, <c>mi</c>, <c>mf</c>,
/// <c>mh</c>, <c>c1</c>, <c>cm</c>, <c>cs</c>, <c>cp</c>. Themes that
/// style Pygments output target these classes verbatim.
/// </remarks>
public static class TokenClassNames
{
    /// <summary>Per-classification UTF-8 class-name table indexed by <see cref="TokenClass"/>.</summary>
    /// <remarks>
    /// Direct array lookup keeps the per-token dispatch branch-free; the
    /// table is built once at type init from the same Pygments-shape
    /// taxonomy that drives the rest of the assembly.
    /// </remarks>
    private static readonly byte[][] CssTable = BuildTable();

    /// <summary>Returns the CSS class for <paramref name="cls"/> as UTF-8 bytes.</summary>
    /// <param name="cls">Token classification.</param>
    /// <returns>UTF-8 class name (no quotes), or empty for plain text.</returns>
    public static ReadOnlySpan<byte> Css(TokenClass cls)
    {
        var idx = (int)cls;
        return (uint)idx < (uint)CssTable.Length ? CssTable[idx] : [];
    }

    /// <summary>Builds the flat lookup table once at type init.</summary>
    /// <returns>Index → class-name byte array.</returns>
    private static byte[][] BuildTable()
    {
        var values = Enum.GetValues<TokenClass>();
        var table = new byte[values.Length][];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = [];
        }

        table[(int)TokenClass.Whitespace] = [.. "w"u8];
        table[(int)TokenClass.Name] = [.. "n"u8];
        table[(int)TokenClass.NameFunction] = [.. "nf"u8];
        table[(int)TokenClass.NameClass] = [.. "nc"u8];
        table[(int)TokenClass.NameBuiltin] = [.. "nb"u8];
        table[(int)TokenClass.NameAttribute] = [.. "na"u8];
        table[(int)TokenClass.Keyword] = [.. "k"u8];
        table[(int)TokenClass.KeywordConstant] = [.. "kc"u8];
        table[(int)TokenClass.KeywordDeclaration] = [.. "kd"u8];
        table[(int)TokenClass.KeywordType] = [.. "kt"u8];
        table[(int)TokenClass.Operator] = [.. "o"u8];
        table[(int)TokenClass.Punctuation] = [.. "p"u8];
        table[(int)TokenClass.String] = [.. "s"u8];
        table[(int)TokenClass.StringSingle] = [.. "s1"u8];
        table[(int)TokenClass.StringDouble] = [.. "s2"u8];
        table[(int)TokenClass.StringEscape] = [.. "se"u8];
        table[(int)TokenClass.NumberInteger] = [.. "mi"u8];
        table[(int)TokenClass.NumberFloat] = [.. "mf"u8];
        table[(int)TokenClass.NumberHex] = [.. "mh"u8];
        table[(int)TokenClass.CommentSingle] = [.. "c1"u8];
        table[(int)TokenClass.CommentMulti] = [.. "cm"u8];
        table[(int)TokenClass.CommentSpecial] = [.. "cs"u8];
        table[(int)TokenClass.CommentPreproc] = [.. "cp"u8];
        return table;
    }
}
