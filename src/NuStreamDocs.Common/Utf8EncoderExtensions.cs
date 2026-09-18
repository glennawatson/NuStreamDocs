// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Common;

/// <summary>Provides UTF-8 conversion extension methods.</summary>
public static class Utf8EncoderExtensions
{
    /// <summary>Extension members for compatibility strings.</summary>
    /// <param name="values">Value to operate on.</param>
    extension(ApiCompatString[]? values)
    {
        /// <summary>Converts the source text to UTF-8 bytes.</summary>
        /// <returns>Converted UTF-8 bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[][] EncodeUtf8Array() => Utf8Encoder.EncodeUtf8Array(values);
    }

    /// <summary>Extension members for strings.</summary>
    /// <param name="values">Value to operate on.</param>
    extension(string[]? values)
    {
        /// <summary>Converts the source text to UTF-8 bytes.</summary>
        /// <returns>Converted UTF-8 bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[][] EncodeUtf8Array() => Utf8Encoder.EncodeUtf8Array(values);
    }
}
