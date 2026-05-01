// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Builder-extension + options tests for <c>WyamBlogPlugin</c>.</summary>
public class BlogRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new WyamBlogPlugin(new("posts", "Blog")).Name).IsEqualTo("wyam-blog");

    /// <summary>2-arg ctor enables EmitTagArchives.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoArgCtorEnablesArchives() =>
        await Assert.That(new WyamBlogOptions("posts", "Blog").EmitTagArchives).IsTrue();

    /// <summary>Validate() throws on empty fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(static () => new WyamBlogOptions(string.Empty, "T").Validate());
        var ex = Assert.Throws<ArgumentException>(static () => new WyamBlogOptions("posts", string.Empty).Validate());
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseWyamBlog(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseWyamBlogRegisters() =>
        await Assert.That(new DocBuilder().UseWyamBlog(new("posts", "Blog"))).IsTypeOf<DocBuilder>();

    /// <summary>UseWyamBlog(options, logger) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseWyamBlogLoggerRegisters() =>
        await Assert.That(new DocBuilder().UseWyamBlog(new("posts", "Blog"), NullLogger.Instance)).IsTypeOf<DocBuilder>();

    /// <summary>UseWyamBlog rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseWyamBlogRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderBlogExtensions.UseWyamBlog(null!, new("posts", "Blog")));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseWyamBlog rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseWyamBlogRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseWyamBlog(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseWyamBlog(options, logger) rejects null logger.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseWyamBlogRejectsNullLogger()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            new DocBuilder().UseWyamBlog(new("posts", "Blog"), null!));
        await Assert.That(ex).IsNotNull();
    }
}
