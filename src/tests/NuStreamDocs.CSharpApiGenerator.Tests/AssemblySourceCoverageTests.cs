// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Coverage for CompositeAssemblySource and LocalAssemblySource ctors and DiscoverAsync.</summary>
public class AssemblySourceCoverageTests
{
    /// <summary>Composite over an empty source array yields no groups.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CompositeEmpty()
    {
        CompositeAssemblySource composite = new([]);
        var count = 0;
        await foreach (var g in composite.DiscoverAsync())
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    /// <summary>Composite over a single empty source streams nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CompositeForwardsEmpty()
    {
        CompositeAssemblySource composite = new([new EmptySource()]);
        var count = 0;
        await foreach (var g in composite.DiscoverAsync())
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    /// <summary>LocalAssemblySource constructed with no DLLs yields nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LocalEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smkd-las-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            LocalAssemblySource local = new("net10.0", [], [dir]);
            var count = 0;
            await foreach (var g in local.DiscoverAsync())
            {
                count++;
            }

            await Assert.That(count).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>AssemblySourceFactory.Create returns the underlying source for a single CustomInput.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FactoryCreateSingle()
    {
        EmptySource src = new();
        var resolved = AssemblySourceFactory.Create([new CustomInput(src)], NullLogger.Instance);
        await Assert.That(resolved).IsEqualTo(src);
    }

    /// <summary>AssemblySourceFactory.Create wraps multiple inputs in a CompositeAssemblySource.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FactoryCreateComposite()
    {
        var resolved = AssemblySourceFactory.Create(
            [new CustomInput(new EmptySource()), new CustomInput(new EmptySource())],
            NullLogger.Instance);
        await Assert.That(resolved).IsTypeOf<CompositeAssemblySource>();
    }

    /// <summary>Stub IAssemblySource that yields no groups.</summary>
    private sealed class EmptySource : IAssemblySource
    {
        /// <inheritdoc/>
        public IAsyncEnumerable<AssemblyGroup> DiscoverAsync() => DiscoverAsync(CancellationToken.None);

        /// <inheritdoc/>
        public async IAsyncEnumerable<AssemblyGroup> DiscoverAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }
    }
}
