// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Blog;

/// <summary>Configuration for <see cref="WyamBlogPlugin"/>.</summary>
/// <param name="PostsSubdirectory">Subdirectory under the docs root that holds the post files (e.g. <c>Announcements</c>).</param>
/// <param name="IndexTitle">Title rendered at the top of the generated index page.</param>
/// <param name="EmitTagArchives">When true, a <c>tags/{tag}.md</c> archive page is generated for each tag in use.</param>
public sealed record WyamBlogOptions(string PostsSubdirectory, string IndexTitle, bool EmitTagArchives)
{
    /// <summary>Initializes a new instance of the <see cref="WyamBlogOptions"/> class with archives enabled.</summary>
    /// <param name="postsSubdirectory">Subdirectory holding posts.</param>
    /// <param name="indexTitle">Index title.</param>
    public WyamBlogOptions(string postsSubdirectory, string indexTitle)
        : this(postsSubdirectory, indexTitle, EmitTagArchives: true)
    {
    }

    /// <summary>Throws when any field is empty.</summary>
    /// <exception cref="ArgumentException">When a required field is null, empty, or whitespace.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PostsSubdirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(IndexTitle);
    }
}
