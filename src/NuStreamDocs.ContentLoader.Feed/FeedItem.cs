// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.ContentLoader.Feed;

/// <summary>One item parsed from an RSS or Atom feed; all fields are UTF-8 bytes and may be empty.</summary>
/// <param name="Title">Item title.</param>
/// <param name="Link">Canonical URL of the item.</param>
/// <param name="Date">Publication date as it appeared in the feed.</param>
/// <param name="Identifier">Stable item identifier — RSS <c>guid</c> or Atom <c>id</c>.</param>
/// <param name="ContentHtml">Item body — Atom <c>content</c> / RSS <c>content:encoded</c>, falling back to <c>summary</c> / <c>description</c>; usually HTML.</param>
internal readonly record struct FeedItem(byte[] Title, byte[] Link, byte[] Date, byte[] Identifier, byte[] ContentHtml);
