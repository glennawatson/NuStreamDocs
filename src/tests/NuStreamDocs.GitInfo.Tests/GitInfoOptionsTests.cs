// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Common;

namespace NuStreamDocs.GitInfo.Tests;

/// <summary>Option fluent mutator, guard clause, and builder extension tests for <see cref="NuStreamDocs.GitInfo"/>.</summary>
public class GitInfoOptionsTests
{
    /// <summary>Every fluent mutator updates its target option and preserves defaults elsewhere.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FluentMutatorsUpdateEveryProperty()
    {
        const int CustomCap = 3;
        const int DefaultExpectedCap = 5;
        StubLogProvider stub = new();

        var custom = GitInfoOptions.Default
            .WithEmitFooter(false)
            .WithEmitContributors(false)
            .WithEmitMetaTag(false)
            .WithEnrichSitemap(false)
            .WithIncludeStylesheet(false)
            .WithMaxContributors(CustomCap)
            .WithLogProvider(stub);

        await Assert.That(GitInfoOptions.DefaultMaxContributors).IsEqualTo(DefaultExpectedCap);
        await Assert.That(custom.EmitFooter).IsFalse();
        await Assert.That(custom.EmitContributors).IsFalse();
        await Assert.That(custom.EmitMetaTag).IsFalse();
        await Assert.That(custom.EnrichSitemap).IsFalse();
        await Assert.That(custom.IncludeStylesheet).IsFalse();
        await Assert.That(custom.MaxContributors).IsEqualTo(CustomCap);
        await Assert.That(custom.LogProvider).IsSameReferenceAs(stub);
    }

    /// <summary>Invalid contributor limits and null providers/callbacks throw argument exceptions.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InvalidArgumentsThrowExpectedExceptions()
    {
        IGitLogProvider? nullProvider = null;
        Func<GitInfoOptions, GitInfoOptions>? nullConfigure = null;
        DocBuilder builder = new();

        await Assert.That(static () => GitInfoOptions.Default.WithMaxContributors(0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => GitInfoOptions.Default.WithMaxContributors(-1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => GitInfoOptions.Default.WithLogProvider(nullProvider!)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.UseGitInfo(nullConfigure!)).Throws<ArgumentNullException>();
    }

    /// <summary>No-op log provider used for reference identity checks.</summary>
    [System.Diagnostics.DebuggerDisplay("StubLogProvider")]
    private sealed class StubLogProvider : IGitLogProvider
    {
        /// <inheritdoc/>
        public ValueTask<byte[]> ReadGitLogAsync(DirectoryPath inputRoot, CancellationToken cancellationToken)
        {
            _ = inputRoot;
            _ = cancellationToken;
            return ValueTask.FromResult<byte[]>([]);
        }
    }
}
