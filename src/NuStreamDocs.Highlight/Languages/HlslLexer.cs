// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>High-Level Shader Language (HLSL) lexer.</summary>
/// <remarks>
/// Direct3D / DirectCompute shader language. C-family lexer with HLSL's
/// dimensioned scalar / vector / matrix types (<c>float4x4</c>, <c>uint3</c>,
/// …), texture / sampler types, and the <c>cbuffer</c> / <c>tbuffer</c>
/// constant-buffer declarations. The C preprocessor is enabled.
/// </remarks>
public static class HlslLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus HLSL's <c>discard</c>.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "discard"u8]]);

    /// <summary>Built-in scalar / vector / matrix / texture / sampler types.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "void"u8],
        [.. "bool"u8],
        [.. "int"u8],
        [.. "uint"u8],
        [.. "half"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "min16float"u8],
        [.. "min10float"u8],
        [.. "min16int"u8],
        [.. "min12int"u8],
        [.. "min16uint"u8],
        [.. "float2"u8],
        [.. "float3"u8],
        [.. "float4"u8],
        [.. "float2x2"u8],
        [.. "float3x3"u8],
        [.. "float4x4"u8],
        [.. "float2x4"u8],
        [.. "float4x2"u8],
        [.. "int2"u8],
        [.. "int3"u8],
        [.. "int4"u8],
        [.. "uint2"u8],
        [.. "uint3"u8],
        [.. "uint4"u8],
        [.. "bool2"u8],
        [.. "bool3"u8],
        [.. "bool4"u8],
        [.. "matrix"u8],
        [.. "vector"u8],
        [.. "Texture1D"u8],
        [.. "Texture2D"u8],
        [.. "Texture3D"u8],
        [.. "TextureCube"u8],
        [.. "Texture2DArray"u8],
        [.. "Texture2DMS"u8],
        [.. "RWTexture1D"u8],
        [.. "RWTexture2D"u8],
        [.. "RWTexture3D"u8],
        [.. "SamplerState"u8],
        [.. "SamplerComparisonState"u8],
        [.. "Buffer"u8],
        [.. "RWBuffer"u8],
        [.. "StructuredBuffer"u8],
        [.. "RWStructuredBuffer"u8],
        [.. "AppendStructuredBuffer"u8],
        [.. "ConsumeStructuredBuffer"u8],
        [.. "ByteAddressBuffer"u8],
        [.. "RWByteAddressBuffer"u8]);

    /// <summary>Storage / qualifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "in"u8],
        [.. "out"u8],
        [.. "inout"u8],
        [.. "const"u8],
        [.. "static"u8],
        [.. "uniform"u8],
        [.. "extern"u8],
        [.. "shared"u8],
        [.. "groupshared"u8],
        [.. "volatile"u8],
        [.. "precise"u8],
        [.. "linear"u8],
        [.. "nointerpolation"u8],
        [.. "noperspective"u8],
        [.. "centroid"u8],
        [.. "sample"u8],
        [.. "row_major"u8],
        [.. "column_major"u8],
        [.. "cbuffer"u8],
        [.. "tbuffer"u8],
        [.. "register"u8],
        [.. "packoffset"u8],
        [.. "struct"u8],
        [.. "class"u8],
        [.. "interface"u8],
        [.. "namespace"u8],
        [.. "typedef"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "NULL"u8]);

    /// <summary>Gets the singleton HLSL lexer.</summary>
    public static Lexer Instance { get; } = CFamilyRules.CreateLexer(new()
    {
        Keywords = Keywords,
        KeywordTypes = KeywordTypes,
        KeywordDeclarations = KeywordDeclarations,
        KeywordConstants = KeywordConstants,
        Operators = CFamilyShared.StandardOperators,
        OperatorFirst = CFamilyShared.StandardOperatorFirst,
        Punctuation = CFamilyShared.StandardPunctuation,
        IntegerSuffix = CFamilyShared.CIntegerSuffix,
        FloatSuffix = CFamilyShared.CFloatSuffix,
        IncludeDocComment = false,
        IncludePreprocessor = true,
        IncludeCharacterLiteral = false,
        WhitespaceIncludesNewlines = true,
        SpecialString = null
    });
}
