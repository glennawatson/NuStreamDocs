// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Blog;

/// <summary>Configuration for <see cref="WyamBlogPlugin"/>.</summary>
/// <param name="PostsSubdirectory">Subdirectory under the docs root that holds the post files (e.g. <c>Announcements</c>).</param>
/// <param name="IndexTitle">UTF-8 title bytes rendered at the top of the generated index page and used as the blog section's nav title.</param>
/// <param name="EmitTagArchives">When true, a <c>tags/{tag}.md</c> archive page is generated for each tag in use.</param>
/// <param name="NavOrder">Optional <c>Order:</c> sort key for the blog section in the navigation; <see langword="null"/> sorts it after explicitly-ordered siblings.</param>
public sealed record WyamBlogOptions(PathSegment PostsSubdirectory, byte[] IndexTitle, bool EmitTagArchives, int? NavOrder)
{
    /// <summary>Initializes a new instance of the <see cref="WyamBlogOptions"/> class with no explicit nav order.</summary>
    /// <param name="postsSubdirectory">Subdirectory holding posts.</param>
    /// <param name="indexTitle">UTF-8 title bytes.</param>
    /// <param name="emitTagArchives">When true, per-tag archive pages are generated.</param>
    public WyamBlogOptions(in PathSegment postsSubdirectory, byte[] indexTitle, bool emitTagArchives)
        : this(postsSubdirectory, indexTitle, emitTagArchives, NavOrder: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WyamBlogOptions"/> class with archives enabled and no explicit nav order.</summary>
    /// <param name="postsSubdirectory">Subdirectory holding posts.</param>
    /// <param name="indexTitle">UTF-8 title bytes.</param>
    public WyamBlogOptions(in PathSegment postsSubdirectory, byte[] indexTitle)
        : this(postsSubdirectory, indexTitle, emitTagArchives: true)
    {
    }

    /// <summary>Throws when any field is empty.</summary>
    /// <exception cref="ArgumentException">When a required field is null or empty.</exception>
    public void Validate()
    {
        if (PostsSubdirectory.IsEmpty)
        {
            throw new ArgumentException("PostsSubdirectory must be non-empty.", nameof(PostsSubdirectory));
        }

        if (IndexTitle is [_, ..])
        {
            return;
        }

        throw new ArgumentException("IndexTitle bytes must be non-empty.", nameof(IndexTitle));
    }
}
