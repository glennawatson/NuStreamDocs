// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Blog.MkDocs;

/// <summary>Configuration for <see cref="MkDocsBlogPlugin"/>.</summary>
/// <param name="BlogSubdirectory">Subdirectory under the docs root that hosts the blog (e.g. <c>blog</c>). Posts live in <c>{BlogSubdirectory}/posts</c>.</param>
/// <param name="IndexTitle">UTF-8 title bytes rendered at the top of the generated index page and used as the blog section's nav title.</param>
/// <param name="EmitCategoryArchives">When true, a <c>{BlogSubdirectory}/category/{slug}.md</c> archive page is generated for each tag/category in use.</param>
/// <param name="NavOrder">Optional <c>Order:</c> sort key for the blog section in the navigation; <see langword="null"/> sorts it after explicitly-ordered siblings.</param>
public sealed record MkDocsBlogOptions(
    PathSegment BlogSubdirectory,
    byte[] IndexTitle,
    bool EmitCategoryArchives,
    int? NavOrder)
{
    /// <summary>Initializes a new instance of the <see cref="MkDocsBlogOptions"/> class with no explicit nav order.</summary>
    /// <param name="blogSubdirectory">Blog subdirectory.</param>
    /// <param name="indexTitle">UTF-8 title bytes.</param>
    /// <param name="emitCategoryArchives">When true, per-category archive pages are generated.</param>
    public MkDocsBlogOptions(in PathSegment blogSubdirectory, byte[] indexTitle, bool emitCategoryArchives)
        : this(blogSubdirectory, indexTitle, emitCategoryArchives, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MkDocsBlogOptions"/> class with archives enabled and no explicit nav order.</summary>
    /// <param name="blogSubdirectory">Blog subdirectory.</param>
    /// <param name="indexTitle">UTF-8 title bytes.</param>
    public MkDocsBlogOptions(in PathSegment blogSubdirectory, byte[] indexTitle)
        : this(blogSubdirectory, indexTitle, true)
    {
    }

    /// <summary>Throws when any field is empty.</summary>
    /// <exception cref="ArgumentException">When a required field is null or empty.</exception>
    public void Validate()
    {
        if (BlogSubdirectory.IsEmpty)
        {
            throw new ArgumentException("BlogSubdirectory must be non-empty.", nameof(BlogSubdirectory));
        }

        if (IndexTitle is [_, ..])
        {
            return;
        }

        throw new ArgumentException("IndexTitle bytes must be non-empty.", nameof(IndexTitle));
    }
}
