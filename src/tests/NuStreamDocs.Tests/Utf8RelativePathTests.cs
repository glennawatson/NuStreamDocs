// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Direct tests for the byte-only relative-URL helper.</summary>
public class Utf8RelativePathTests
{
    /// <summary>Identical inputs collapse to a self-reference so the link still resolves.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SelfReferenceWhenIdentical()
    {
        await Assert.That(Compute("articles", "articles")).IsEqualTo(".");
    }

    /// <summary>A target inside the source directory is emitted as a sibling.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TargetInsideFromIsSibling()
    {
        await Assert.That(Compute("articles", "articles/post.md")).IsEqualTo("post.md");
    }

    /// <summary>A target outside the source directory walks up via <c>../</c> for each unmatched segment.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TargetOutsideFromUsesParentSegments()
    {
        await Assert.That(Compute("articles/tags", "articles/post.md")).IsEqualTo("../post.md");
    }

    /// <summary>Two unmatched segments produce two <c>../</c> hops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoUnmatchedSegmentsTwoParents()
    {
        await Assert.That(Compute("articles/tags/2024", "other/post.md")).IsEqualTo("../../../other/post.md");
    }

    /// <summary>A sibling-directory target needs one parent hop and the target's directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SiblingDirectoryTarget()
    {
        await Assert.That(Compute("blog/posts", "blog/category/dotnet.md")).IsEqualTo("../category/dotnet.md");
    }

    /// <summary>An empty source directory passes the target through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyFromPassesTargetThrough()
    {
        await Assert.That(Compute(string.Empty, "post.md")).IsEqualTo("post.md");
    }

    /// <summary>An empty source directory and empty target both collapse to a self-reference.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyFromAndTargetSelfReference()
    {
        await Assert.That(Compute(string.Empty, string.Empty)).IsEqualTo(".");
    }

    /// <summary>A subdirectory target one level deeper resolves without parent hops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubdirectoryTargetIsBareRelativePath()
    {
        await Assert.That(Compute("blog", "blog/posts/intro.md")).IsEqualTo("posts/intro.md");
    }

    /// <summary>A target that lives in a different first-level directory walks up the entire source chain.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DisjointTargetWalksUpFullFrom()
    {
        await Assert.That(Compute("blog/posts", "guide/intro.md")).IsEqualTo("../../guide/intro.md");
    }

    /// <summary>A target sitting at the docs root from a deep directory walks back up to the root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RootTargetFromDeepFrom()
    {
        await Assert.That(Compute("blog/posts", "index.md")).IsEqualTo("../../index.md");
    }

    /// <summary>Sink null-checks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullSinkThrows() =>
        await Assert.That(() => Utf8RelativePath.WriteRelative(null!, "a"u8, "b"u8))
            .Throws<ArgumentNullException>();

    /// <summary>Convenience wrapper that runs WriteRelative against UTF-8 string inputs.</summary>
    /// <param name="from">Forward-slashed source-directory text.</param>
    /// <param name="target">Forward-slashed target-path text.</param>
    /// <returns>The emitted relative URL as a managed string for assertion.</returns>
    private static string Compute(string from, string target)
    {
        ArrayBufferWriter<byte> sink = new();
        Utf8RelativePath.WriteRelative(sink, Encoding.UTF8.GetBytes(from), Encoding.UTF8.GetBytes(target));
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
