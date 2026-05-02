// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Test-only helpers that adapt string literals to the byte-shaped privacy APIs.</summary>
internal static class PrivacyTestHelpers
{
    /// <summary>Encodes <paramref name="values"/> into a UTF-8 byte-array array.</summary>
    /// <param name="values">Source strings.</param>
    /// <returns>Byte-array array, one entry per input string.</returns>
    public static byte[][] Utf8(params string[] values) =>
        Array.ConvertAll(values, Encoding.UTF8.GetBytes);
}
