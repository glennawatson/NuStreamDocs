// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Common;

/// <summary>Provides UTF-8 conversion extension methods.</summary>
public static class Utf8PathHumanizerExtensions
{
    /// <summary>Extension members for <c>ReadOnlySpan&lt;char&gt;</c>.</summary>
    /// <param name="name">Value to operate on.</param>
    extension(in ReadOnlySpan<char> name)
    {
        /// <summary>Converts the source text to UTF-8 bytes.</summary>
        /// <returns>Converted UTF-8 bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] HumanizePathName() => Utf8PathHumanizer.HumanizePathName(name);
    }
}
