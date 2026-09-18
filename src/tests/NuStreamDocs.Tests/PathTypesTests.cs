// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>DirectoryPath</c> and <c>FilePath</c> primitives.</summary>
public class PathTypesTests
{
    /// <summary>Docs Directory used by the test cases.</summary>
    private const string DocsDirectory = "/docs";

    /// <summary>Guide Directory used by the test cases.</summary>
    private const string GuideDirectory = "guide";

    /// <summary>Intro File Name used by the test cases.</summary>
    private const string IntroFileName = "intro.md";

    /// <summary>Intro Path used by the test cases.</summary>
    private const string IntroPath = "/docs/intro.md";

    /// <summary>Default-constructed paths report empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultIsEmpty()
    {
        await Assert.That(default(DirectoryPath).IsEmpty).IsTrue();
        await Assert.That(default(FilePath).IsEmpty).IsTrue();
    }

    /// <summary>Combine and / operator both produce a child directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CombineProducesChildDirectory()
    {
        DirectoryPath root = new(DocsDirectory);
        await Assert.That((root / GuideDirectory).Value).IsEqualTo(Path.Combine(DocsDirectory, GuideDirectory));
    }

    /// <summary>The <see cref="DirectoryPath.File(string)"/> helper composes a file path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FileHelperComposesFilePath()
    {
        DirectoryPath root = new(DocsDirectory);
        var page = root.File(IntroFileName);
        await Assert.That(page.Value).IsEqualTo(Path.Combine(DocsDirectory, IntroFileName));
        await Assert.That(page.FileName).IsEqualTo(IntroFileName);
        await Assert.That(page.FileNameWithoutExtension).IsEqualTo("intro");
        await Assert.That(page.Extension).IsEqualTo(".md");
    }

    /// <summary><see cref="FilePath.Directory"/> returns the parent.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FileDirectoryReturnsParent()
    {
        var pageValue = Path.Combine(DocsDirectory, GuideDirectory, IntroFileName);
        FilePath page = new(pageValue);
        await Assert.That(page.Directory.Value).IsEqualTo(Path.GetDirectoryName(pageValue));
    }

    /// <summary><see cref="FilePath.WithExtension(string)"/> swaps the file extension in place.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExtensionRenamesFile()
    {
        FilePath page = new(IntroPath);
        var html = page.WithExtension(".html");
        await Assert.That(html.Value).IsEqualTo(Path.ChangeExtension(IntroPath, ".html"));
    }

    /// <summary>Implicit conversion to string lets path types feed BCL APIs unmodified.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ImplicitStringConversion()
    {
        DirectoryPath dir = new(DocsDirectory);
        await Assert.That((string)dir).IsEqualTo(DocsDirectory);

        FilePath file = new(IntroPath);
        await Assert.That((string)file).IsEqualTo(IntroPath);
    }

    /// <summary>Equality is value-based for both types.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValueEqualityHolds()
    {
        await Assert.That(new DirectoryPath("/a")).IsEqualTo(new("/a"));
        await Assert.That(new FilePath("/a/b.md")).IsEqualTo(new("/a/b.md"));
    }

    /// <summary>Implicit conversion from <see cref="string"/> wraps the value so callers can pass string literals.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ImplicitFromStringWraps()
    {
        DirectoryPath dir = DocsDirectory;
        FilePath file = IntroPath;
        await Assert.That(dir.Value).IsEqualTo(DocsDirectory);
        await Assert.That(file.Value).IsEqualTo(IntroPath);
    }

    /// <summary>A null string converts to an empty path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ImplicitFromNullStringIsEmpty()
    {
        DirectoryPath dir = (string?)null;
        FilePath file = (string?)null;
        await Assert.That(dir.IsEmpty).IsTrue();
        await Assert.That(file.IsEmpty).IsTrue();
    }

    /// <summary>UrlPath wraps and unwraps URL strings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlPathWraps()
    {
        UrlPath relative = "/assets/extra/foo.css";
        UrlPath absolute = "https://example.com/foo.css";
        UrlPath protoRelative = "//cdn.example.com/foo.css";
        await Assert.That(relative.Value).IsEqualTo("/assets/extra/foo.css");
        await Assert.That((string)absolute).IsEqualTo("https://example.com/foo.css");
        await Assert.That(relative.IsAbsolute).IsFalse();
        await Assert.That(absolute.IsAbsolute).IsTrue();
        await Assert.That(protoRelative.IsAbsolute).IsTrue();
    }

    /// <summary>PathSegment wraps relative subdirectory references.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PathSegmentWraps()
    {
        PathSegment segment = "blog/posts";
        await Assert.That(segment.Value).IsEqualTo("blog/posts");
        await Assert.That(segment.IsEmpty).IsFalse();
    }

    /// <summary>GlobPattern wraps include/exclude glob strings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GlobPatternWraps()
    {
        GlobPattern pattern = "**/*.md";
        await Assert.That(pattern.Value).IsEqualTo("**/*.md");
        await Assert.That(pattern.IsEmpty).IsFalse();
    }
}
