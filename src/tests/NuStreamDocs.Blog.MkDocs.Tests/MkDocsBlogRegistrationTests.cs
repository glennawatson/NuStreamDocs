// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Blog.MkDocs.Tests;

/// <summary>Builder-extension + options tests for <c>MkDocsBlogPlugin</c>.</summary>
public class MkDocsBlogRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new MkDocsBlogPlugin(new MkDocsBlogOptions("blog", "Blog")).Name).IsEqualTo("mkdocs-blog");

    /// <summary>2-arg ctor enables EmitCategoryArchives.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoArgCtorEnablesArchives() =>
        await Assert.That(new MkDocsBlogOptions("blog", "Blog").EmitCategoryArchives).IsTrue();

    /// <summary>Validate() throws on empty fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(static () => new MkDocsBlogOptions(string.Empty, "T").Validate());
        var ex = Assert.Throws<ArgumentException>(static () => new MkDocsBlogOptions("blog", string.Empty).Validate());
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseMkDocsBlog(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMkDocsBlogRegisters() =>
        await Assert.That(new DocBuilder().UseMkDocsBlog(new MkDocsBlogOptions("blog", "Blog"))).IsTypeOf<DocBuilder>();

    /// <summary>UseMkDocsBlog(options, logger) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMkDocsBlogLoggerRegisters() =>
        await Assert.That(new DocBuilder().UseMkDocsBlog(new MkDocsBlogOptions("blog", "Blog"), NullLogger.Instance)).IsTypeOf<DocBuilder>();

    /// <summary>UseMkDocsBlog rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMkDocsBlogRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderMkDocsBlogExtensions.UseMkDocsBlog(null!, new MkDocsBlogOptions("blog", "Blog")));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseMkDocsBlog rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMkDocsBlogRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseMkDocsBlog(null!));
        await Assert.That(ex).IsNotNull();
    }
}
