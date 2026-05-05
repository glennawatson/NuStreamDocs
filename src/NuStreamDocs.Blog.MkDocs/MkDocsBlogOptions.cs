// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Blog.MkDocs;

/// <summary>Configuration for <see cref="MkDocsBlogPlugin"/>.</summary>
/// <param name="BlogSubdirectory">Subdirectory under the docs root that hosts the blog (e.g. <c>blog</c>). Posts live in <c>{BlogSubdirectory}/posts</c>.</param>
/// <param name="IndexTitle">UTF-8 title bytes rendered at the top of the generated index page.</param>
/// <param name="EmitCategoryArchives">When true, a <c>{BlogSubdirectory}/category/{slug}.md</c> archive page is generated for each tag/category in use.</param>
public sealed record MkDocsBlogOptions(PathSegment BlogSubdirectory, byte[] IndexTitle, bool EmitCategoryArchives)
{
    /// <summary>Initializes a new instance of the <see cref="MkDocsBlogOptions"/> class with archives enabled.</summary>
    /// <param name="blogSubdirectory">Blog subdirectory.</param>
    /// <param name="indexTitle">UTF-8 title bytes.</param>
    public MkDocsBlogOptions(PathSegment blogSubdirectory, byte[] indexTitle)
        : this(blogSubdirectory, indexTitle, EmitCategoryArchives: true)
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
