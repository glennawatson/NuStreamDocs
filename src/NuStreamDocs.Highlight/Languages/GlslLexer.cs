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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "discard"u8);

    /// <summary>Built-in primitive / vector / matrix / sampler type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "void bool int uint float double"u8,
        "vec2 vec3 vec4 ivec2 ivec3 ivec4 uvec2 uvec3 uvec4 bvec2 bvec3 bvec4 dvec2 dvec3 dvec4"u8,
        "mat2 mat3 mat4 mat2x2 mat2x3 mat2x4 mat3x2 mat3x3 mat3x4 mat4x2 mat4x3 mat4x4"u8,
        "sampler1D sampler2D sampler3D samplerCube sampler2DArray sampler2DShadow samplerCubeShadow image2D image3D imageCube atomic_uint"u8);

    /// <summary>Storage / interface / qualifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "attribute uniform varying buffer shared in out inout const centroid sample patch flat smooth noperspective layout"u8,
        "precision highp mediump lowp invariant coherent volatile restrict readonly writeonly subroutine struct"u8);

    /// <summary>Constant keywords — GLSL has only <c>true</c> / <c>false</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false"u8);

    /// <summary>Gets the singleton GLSL lexer.</summary>
    public static Lexer Instance { get; } = CFamilyRules.CreateLexer(new()
    {
        Keywords = Keywords,
        KeywordTypes = KeywordTypes,
        KeywordDeclarations = KeywordDeclarations,
        KeywordConstants = KeywordConstants,
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
