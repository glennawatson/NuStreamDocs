// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Feed.Tests;

/// <summary>Builder-extension + options tests for <c>FeedPlugin</c>.</summary>
public class FeedRegistrationTests
{
    /// <summary>Default 4-arg ctor uses the recommended item cap.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultCtorUsesRecommendedCap()
    {
        var opts = new FeedOptions("https://x.test/", "T", "D", "blog");
        await Assert.That(opts.MaxItems).IsEqualTo(FeedOptions.DefaultMaxItems);
        await Assert.That(opts.Formats).IsEqualTo(FeedFormats.Both);
    }

    /// <summary>Validate() throws on each missing required field.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnMissingFields()
    {
        Assert.Throws<ArgumentException>(static () => new FeedOptions(string.Empty, "T", "D", "p", "o", FeedFormats.Both, 1).Validate());
        Assert.Throws<ArgumentException>(static () => new FeedOptions("u", string.Empty, "D", "p", "o", FeedFormats.Both, 1).Validate());
        Assert.Throws<ArgumentException>(static () => new FeedOptions("u", "T", string.Empty, "p", "o", FeedFormats.Both, 1).Validate());
        Assert.Throws<ArgumentException>(static () => new FeedOptions("u", "T", "D", string.Empty, "o", FeedFormats.Both, 1).Validate());
        var ex = Assert.Throws<ArgumentException>(static () => new FeedOptions("u", "T", "D", "p", string.Empty, FeedFormats.Both, 1).Validate());
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseFeed(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseFeedRegisters()
    {
        var opts = new FeedOptions("https://x.test/", "T", "D", "blog");
        await Assert.That(new DocBuilder().UseFeed(opts)).IsTypeOf<DocBuilder>();
    }

    /// <summary>UseFeed(options, logger) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseFeedLoggerRegisters()
    {
        var opts = new FeedOptions("https://x.test/", "T", "D", "blog");
        await Assert.That(new DocBuilder().UseFeed(opts, NullLogger.Instance)).IsTypeOf<DocBuilder>();
    }

    /// <summary>UseFeed rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseFeedRejectsNullBuilder()
    {
        var opts = new FeedOptions("https://x.test/", "T", "D", "blog");
        var ex = Assert.Throws<ArgumentNullException>(() => DocBuilderFeedExtensions.UseFeed(null!, opts));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseFeed rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseFeedRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseFeed(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseFeed(options, logger) rejects null logger.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseFeedLoggerRejectsNullLogger()
    {
        var opts = new FeedOptions("https://x.test/", "T", "D", "blog");
        var ex = Assert.Throws<ArgumentNullException>(() => new DocBuilder().UseFeed(opts, null!));
        await Assert.That(ex).IsNotNull();
    }
}
