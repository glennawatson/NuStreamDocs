// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search;

/// <summary>One page's contribution to the search index.</summary>
/// <param name="RelativeUrl">URL relative to the site root, forward-slashed (e.g. <c>guide/intro.html</c>), UTF-8.</param>
/// <param name="Title">Plain-text page title, UTF-8.</param>
/// <param name="Text">Plain-text body with HTML tags stripped, UTF-8.</param>
public readonly record struct SearchDocument(
    byte[] RelativeUrl,
    byte[] Title,
    byte[] Text);
