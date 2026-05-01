// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Branch-coverage tests for BlogSlugifier.Slugify.</summary>
public class BlogSlugifierTests
{
    /// <summary>Lowercase letters and digits pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LowercaseAndDigits() =>
        await Assert.That(BlogSlugifier.Slugify("abc123", "fallback")).IsEqualTo("abc123");

    /// <summary>Uppercase letters are case-folded to lowercase.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UppercaseFolded() =>
        await Assert.That(BlogSlugifier.Slugify("HelloWorld", "fb")).IsEqualTo("helloworld");

    /// <summary>Hyphen and underscore are preserved.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HyphenUnderscorePreserved() =>
        await Assert.That(BlogSlugifier.Slugify("a-b_c", "fb")).IsEqualTo("a-b_c");

    /// <summary>Spaces and slashes become hyphens.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpacesAndSlashesBecomeHyphens() =>
        await Assert.That(BlogSlugifier.Slugify("foo bar/baz", "fb")).IsEqualTo("foo-bar-baz");

    /// <summary>Unrecognised characters are dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PunctuationDropped() =>
        await Assert.That(BlogSlugifier.Slugify("hi! @world", "fb")).IsEqualTo("hi-world");

    /// <summary>An entirely-unsafe input returns the fallback.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FallbackOnEmptyResult() =>
        await Assert.That(BlogSlugifier.Slugify("@@@", "tag")).IsEqualTo("tag");

    /// <summary>Empty input returns the fallback.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyReturnsFallback() =>
        await Assert.That(BlogSlugifier.Slugify(string.Empty, "fb")).IsEqualTo("fb");
}
