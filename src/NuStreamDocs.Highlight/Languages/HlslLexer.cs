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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "discard"u8);

    /// <summary>Built-in scalar / vector / matrix / texture / sampler types.</summary>
    private static readonly ByteKeywordSet KeywordTypes = BuildKeywordTypes();

    /// <summary>Storage / qualifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "in out inout const static uniform extern shared groupshared volatile precise linear nointerpolation"u8,
        "noperspective centroid sample row_major column_major cbuffer tbuffer register packoffset struct class interface namespace typedef"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false NULL"u8);

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

    /// <summary>Builds the HLSL type keyword set across four UTF-8 chunks (scalars, vectors, matrices, resource handles).</summary>
    /// <returns>HLSL type keyword set.</returns>
    private static ByteKeywordSet BuildKeywordTypes() => ByteKeywordSet.CreateFromSpaceSeparated(
        "void bool int uint half float double min16float min10float min16int min12int min16uint"u8,
        "float2 float3 float4 float2x2 float3x3 float4x4 float2x4 float4x2 int2 int3 int4 uint2 uint3 uint4 bool2 bool3 bool4 matrix vector"u8,
        "Texture1D Texture2D Texture3D TextureCube Texture2DArray Texture2DMS RWTexture1D RWTexture2D RWTexture3D SamplerState SamplerComparisonState"u8,
        "Buffer RWBuffer StructuredBuffer RWStructuredBuffer AppendStructuredBuffer ConsumeStructuredBuffer ByteAddressBuffer RWByteAddressBuffer"u8);
}
