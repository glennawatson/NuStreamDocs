// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Feed;

/// <summary>Configuration for <see cref="FeedPlugin"/>.</summary>
/// <param name="SiteUrl">Absolute URL the site is published at; used to build per-post link/guid.</param>
/// <param name="Title">Feed title; usually the site or blog title.</param>
/// <param name="Description">Short description of the feed contents.</param>
/// <param name="PostsSubdirectory">Source subdirectory under the docs root that holds the post files.</param>
/// <param name="OutputSubdirectory">Output subdirectory (under the site root) where the feed files are written.</param>
/// <param name="Formats">Which formats to generate.</param>
/// <param name="MaxItems">Cap on the number of items included; 0 means no cap.</param>
/// <remarks>The <c>20</c>-item default mirrors mkdocs-rss-plugin's recommended cap.</remarks>
public sealed record FeedOptions(
    string SiteUrl,
    string Title,
    string Description,
    PathSegment PostsSubdirectory,
    PathSegment OutputSubdirectory,
    FeedFormats Formats,
    int MaxItems)
{
    /// <summary>Default per-feed item cap. Picked to match mkdocs-rss-plugin's recommended setting.</summary>
    private const int DefaultMaxItemsValue = 20;

    /// <summary>Initializes a new instance of the <see cref="FeedOptions"/> class with both formats and the default item cap.</summary>
    /// <param name="siteUrl">Absolute site URL.</param>
    /// <param name="title">Feed title.</param>
    /// <param name="description">Feed description.</param>
    /// <param name="postsSubdirectory">Posts subdirectory.</param>
    public FeedOptions(string siteUrl, string title, string description, PathSegment postsSubdirectory)
        : this(siteUrl, title, description, postsSubdirectory, postsSubdirectory, FeedFormats.Both, DefaultMaxItemsValue)
    {
    }

    /// <summary>Gets the default per-feed item cap; mirrors mkdocs-rss-plugin's default.</summary>
    public static int DefaultMaxItems => DefaultMaxItemsValue;

    /// <summary>Throws when any required field is empty.</summary>
    /// <exception cref="ArgumentException">When a required field is null, empty, or whitespace.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SiteUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(Description);
        ArgumentException.ThrowIfNullOrEmpty(PostsSubdirectory.Value);
        ArgumentException.ThrowIfNullOrEmpty(OutputSubdirectory.Value);
    }
}
