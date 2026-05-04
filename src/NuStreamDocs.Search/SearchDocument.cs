// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search;

/// <summary>One page's contribution to the search index.</summary>
/// <remarks>
/// Records the page's relative URL plus the title (first H1) and the
/// full text body, both as UTF-8 byte arrays. Storing UTF-8 bytes
/// lets the index writers feed them through
/// <see cref="System.Text.Json.Utf8JsonWriter"/>'s byte-span
/// overloads with no UTF-16 round-trip.
/// </remarks>
/// <param name="RelativeUrl">URL relative to the site root, forward-slashed (e.g. <c>guide/intro.html</c>), UTF-8.</param>
/// <param name="Title">Plain-text page title, UTF-8.</param>
/// <param name="Text">Plain-text body, UTF-8, with HTML tags stripped.</param>
public readonly record struct SearchDocument(
    byte[] RelativeUrl,
    byte[] Title,
    byte[] Text);
