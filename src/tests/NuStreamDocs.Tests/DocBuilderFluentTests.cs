// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Tests;

/// <summary>Surface tests for <c>DocBuilder</c>'s fluent setters + filter assembly.</summary>
public class DocBuilderFluentTests
{
    /// <summary>UseDirectoryUrls() flips the flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseDirectoryUrlsTrue() =>
        await Assert.That(new DocBuilder().UseDirectoryUrls().UseDirectoryUrlsEnabled).IsTrue();

    /// <summary>UseDirectoryUrls(false) keeps the flag off.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseDirectoryUrlsFalse() =>
        await Assert.That(new DocBuilder().UseDirectoryUrls(false).UseDirectoryUrlsEnabled).IsFalse();

    /// <summary>IncludeDrafts() flips the flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncludeDraftsTrue() =>
        await Assert.That(new DocBuilder().IncludeDrafts().IncludeDraftsEnabled).IsTrue();

    /// <summary>IncludeDrafts(false) keeps the flag off.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncludeDraftsFalse() =>
        await Assert.That(new DocBuilder().IncludeDrafts(false).IncludeDraftsEnabled).IsFalse();

    /// <summary>WithLogger rejects null.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithLoggerRejectsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().WithLogger(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>WithLogger chains.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithLoggerChains()
    {
        DocBuilder builder = new();
        await Assert.That(builder.WithLogger(NullLogger.Instance)).IsSameReferenceAs(builder);
    }

    /// <summary>WithInput rejects empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithInputRejectsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(static () => new DocBuilder().WithInput(string.Empty));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>WithOutput rejects empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithOutputRejectsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(static () => new DocBuilder().WithOutput(string.Empty));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>WithInput/WithOutput chain.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithInputOutputChain()
    {
        DocBuilder builder = new();
        await Assert.That(builder.WithInput("/in")).IsSameReferenceAs(builder);
        await Assert.That(builder.WithOutput("/out")).IsSameReferenceAs(builder);
    }

    /// <summary>Include rejects empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncludeRejectsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(static () => new DocBuilder().Include(string.Empty));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Include(params) rejects empty entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncludeParamsRejectsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(static () => new DocBuilder().Include("ok", string.Empty));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Exclude rejects empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExcludeRejectsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(static () => new DocBuilder().Exclude(string.Empty));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Exclude(params) rejects empty entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExcludeParamsRejectsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(static () => new DocBuilder().Exclude("ok", string.Empty));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>BuildPathFilter returns Empty when nothing was registered.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildPathFilterEmptyDefault() =>
        await Assert.That(new DocBuilder().BuildPathFilter()).IsSameReferenceAs(PathFilter.Empty);

    /// <summary>BuildPathFilter returns a real filter when includes/excludes were registered.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildPathFilterReturnsConfigured()
    {
        var filter = new DocBuilder().Include("**/*.md").Exclude("drafts/**").BuildPathFilter();
        await Assert.That(filter).IsNotSameReferenceAs(PathFilter.Empty);
        await Assert.That(filter.HasRules).IsTrue();
    }

    /// <summary>UsePlugin(IPlugin) rejects null.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UsePluginRejectsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UsePlugin(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>GetOrAddPlugin returns the same instance on repeat calls.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GetOrAddPluginIdempotent()
    {
        DocBuilder builder = new();
        var first = builder.GetOrAddPlugin<RecordingPlugin>();
        var second = builder.GetOrAddPlugin<RecordingPlugin>();
        await Assert.That(first).IsSameReferenceAs(second);
    }

    /// <summary>RenderPageAsync rejects a null html sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RenderPageRejectsNullHtml()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(static () =>
            new DocBuilder().RenderPageAsync("p.md", default, null!, CancellationToken.None));
        await Assert.That(ex).IsNotNull();
    }
}
