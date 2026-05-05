// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search;

/// <summary>Produces the <c>page-N</c> fallback slug used by <see cref="PagefindIndexWriter"/> when a document has no usable URL.</summary>
internal static class PagefindFallbackSlug
{
    /// <summary>Gets the UTF-8 prefix shared by every fallback slug.</summary>
    private static ReadOnlySpan<byte> Prefix => "page-"u8;

    /// <summary>Returns <c>page-{ordinal}</c> formatted with invariant culture as UTF-8 bytes.</summary>
    /// <param name="ordinal">Document ordinal.</param>
    /// <returns>Slug bytes.</returns>
    public static byte[] For(int ordinal)
    {
        Span<byte> digits = stackalloc byte[16];

        // 16 bytes fits int.MinValue with sign; the false branch is unreachable for int but keeps the API safe.
        return ordinal.TryFormat(digits, out var written, provider: CultureInfo.InvariantCulture)
            ? Utf8Concat.Concat(Prefix, digits[..written])
            : Utf8Concat.Concat(Prefix, System.Text.Encoding.UTF8.GetBytes(ordinal.ToString(CultureInfo.InvariantCulture)));
    }
}
