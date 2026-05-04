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
        await Assert.That(BlogSlugifier.Slugify("abc123"u8, "fallback"u8).AsSpan().SequenceEqual("abc123"u8)).IsTrue();

    /// <summary>Uppercase letters are case-folded to lowercase.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UppercaseFolded() =>
        await Assert.That(BlogSlugifier.Slugify("HelloWorld"u8, "fb"u8).AsSpan().SequenceEqual("helloworld"u8)).IsTrue();

    /// <summary>Hyphen and underscore are preserved.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HyphenUnderscorePreserved() =>
        await Assert.That(BlogSlugifier.Slugify("a-b_c"u8, "fb"u8).AsSpan().SequenceEqual("a-b_c"u8)).IsTrue();

    /// <summary>Spaces and slashes become hyphens.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpacesAndSlashesBecomeHyphens() =>
        await Assert.That(BlogSlugifier.Slugify("foo bar/baz"u8, "fb"u8).AsSpan().SequenceEqual("foo-bar-baz"u8)).IsTrue();

    /// <summary>Unrecognized characters are dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PunctuationDropped() =>
        await Assert.That(BlogSlugifier.Slugify("hi! @world"u8, "fb"u8).AsSpan().SequenceEqual("hi-world"u8)).IsTrue();

    /// <summary>An entirely-unsafe input returns the fallback.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FallbackOnEmptyResult() =>
        await Assert.That(BlogSlugifier.Slugify("@@@"u8, "tag"u8).AsSpan().SequenceEqual("tag"u8)).IsTrue();

    /// <summary>Empty input returns the fallback.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyReturnsFallback() =>
        await Assert.That(BlogSlugifier.Slugify([], "fb"u8).AsSpan().SequenceEqual("fb"u8)).IsTrue();
}
