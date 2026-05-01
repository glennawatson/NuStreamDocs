// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Search;

/// <summary>Produces the <c>page-N</c> fallback slug used by <see cref="PagefindIndexWriter"/> when a document has no usable URL.</summary>
internal static class PagefindFallbackSlug
{
    /// <summary>Returns <c>page-{ordinal}</c> formatted with invariant culture.</summary>
    /// <param name="ordinal">Document ordinal.</param>
    /// <returns>Slug string.</returns>
    public static string For(int ordinal) => "page-" + ordinal.ToString(CultureInfo.InvariantCulture);
}
