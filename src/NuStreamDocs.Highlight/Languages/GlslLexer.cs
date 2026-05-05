// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>OpenGL Shading Language (GLSL) lexer.</summary>
/// <remarks>
/// C-family lexer with the GLSL-specific built-in vector / matrix / sampler
/// type set, storage qualifiers (<c>attribute</c> / <c>uniform</c> / <c>varying</c>
/// / <c>in</c> / <c>out</c> / <c>inout</c>), and the <c>#version</c> /
/// <c>#extension</c> preprocessor directives. Built-in functions classify as
/// general identifiers — the keyword tables stay focused on the language's
/// reserved-word surface.
/// </remarks>
public static class GlslLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus GLSL's <c>discard</c>.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "discard"u8]]);

    /// <summary>Built-in primitive / vector / matrix / sampler type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "void"u8],
        [.. "bool"u8],
        [.. "int"u8],
        [.. "uint"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "vec2"u8],
        [.. "vec3"u8],
        [.. "vec4"u8],
        [.. "ivec2"u8],
        [.. "ivec3"u8],
        [.. "ivec4"u8],
        [.. "uvec2"u8],
        [.. "uvec3"u8],
        [.. "uvec4"u8],
        [.. "bvec2"u8],
        [.. "bvec3"u8],
        [.. "bvec4"u8],
        [.. "dvec2"u8],
        [.. "dvec3"u8],
        [.. "dvec4"u8],
        [.. "mat2"u8],
        [.. "mat3"u8],
        [.. "mat4"u8],
        [.. "mat2x2"u8],
        [.. "mat2x3"u8],
        [.. "mat2x4"u8],
        [.. "mat3x2"u8],
        [.. "mat3x3"u8],
        [.. "mat3x4"u8],
        [.. "mat4x2"u8],
        [.. "mat4x3"u8],
        [.. "mat4x4"u8],
        [.. "sampler1D"u8],
        [.. "sampler2D"u8],
        [.. "sampler3D"u8],
        [.. "samplerCube"u8],
        [.. "sampler2DArray"u8],
        [.. "sampler2DShadow"u8],
        [.. "samplerCubeShadow"u8],
        [.. "image2D"u8],
        [.. "image3D"u8],
        [.. "imageCube"u8],
        [.. "atomic_uint"u8]);

    /// <summary>Storage / interface / qualifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "attribute"u8],
        [.. "uniform"u8],
        [.. "varying"u8],
        [.. "buffer"u8],
        [.. "shared"u8],
        [.. "in"u8],
        [.. "out"u8],
        [.. "inout"u8],
        [.. "const"u8],
        [.. "centroid"u8],
        [.. "sample"u8],
        [.. "patch"u8],
        [.. "flat"u8],
        [.. "smooth"u8],
        [.. "noperspective"u8],
        [.. "layout"u8],
        [.. "precision"u8],
        [.. "highp"u8],
        [.. "mediump"u8],
        [.. "lowp"u8],
        [.. "invariant"u8],
        [.. "coherent"u8],
        [.. "volatile"u8],
        [.. "restrict"u8],
        [.. "readonly"u8],
        [.. "writeonly"u8],
        [.. "subroutine"u8],
        [.. "struct"u8]);

    /// <summary>Constant keywords — GLSL has only <c>true</c> / <c>false</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8]);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("bcdefirstw"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abdfimsuv"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("abciflmnoprsuvw"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tf"u8);

    /// <summary>Gets the singleton GLSL lexer.</summary>
    public static Lexer Instance { get; } = CFamilyRules.CreateLexer(new()
    {
        Keywords = Keywords,
        KeywordFirst = KeywordFirst,
        KeywordTypes = KeywordTypes,
        KeywordTypeFirst = KeywordTypeFirst,
        KeywordDeclarations = KeywordDeclarations,
        KeywordDeclarationFirst = KeywordDeclarationFirst,
        KeywordConstants = KeywordConstants,
        KeywordConstantFirst = KeywordConstantFirst,
        Operators = CFamilyShared.StandardOperators,
        OperatorFirst = CFamilyShared.StandardOperatorFirst,
        Punctuation = CFamilyShared.StandardPunctuation,
        IntegerSuffix = SearchValues.Create("uU"u8),
        FloatSuffix = SearchValues.Create("fFlL"u8),
        IncludeDocComment = false,
        IncludePreprocessor = true,
        IncludeCharacterLiteral = false,
        WhitespaceIncludesNewlines = true,
        SpecialString = null
    });
}
