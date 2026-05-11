// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Coverage for CSharpApiGeneratorPlugin Name + DiscoverAsync, FromSource, CustomInput, builder extensions.</summary>
public class CSharpApiGeneratorPluginCoverageTests
{
    /// <summary>Name returns the registered string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        CSharpApiGeneratorPlugin plugin = new(CSharpApiGeneratorOptions.FromSource(new EmptySource()));
        await Assert.That(plugin.Name.SequenceEqual("csharp-apigenerator"u8)).IsTrue();
    }

    /// <summary>CustomInput stores the supplied source.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomInputCtor()
    {
        EmptySource src = new();
        CustomInput input = new(src);
        await Assert.That(input.Source).IsEqualTo(src);
    }

    /// <summary>Builder UseCSharpApiGenerator overloads register the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuilderExtensionsRegister()
    {
        var opts = CSharpApiGeneratorOptions.FromSource(new EmptySource());
        var b1 = new DocBuilder().UseCSharpApiGenerator(opts);
        var b2 = new DocBuilder().UseCSharpApiGenerator(opts, NullLogger.Instance);
        var b3 = new DocBuilder().UseCSharpApiGeneratorDirect(opts);
        await Assert.That(b1).IsNotNull();
        await Assert.That(b2).IsNotNull();
        await Assert.That(b3).IsNotNull();
    }

    /// <summary>Before <c>DiscoverAsync</c> the plugin reports no synthetic nav entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoSyntheticNavEntriesBeforeDiscover()
    {
        CSharpApiGeneratorPlugin plugin = new(CSharpApiGeneratorOptions.FromSource(new EmptySource()));
        await Assert.That(plugin.SyntheticNavEntries.Count).IsEqualTo(0);
    }

    /// <summary><c>DiscoverAsync</c> pre-seeds the <c>api/index.md</c> nav entry carrying the configured title and order, even when generation produces no pages.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DiscoverPublishesIndexNavEntry()
    {
        CSharpApiGeneratorPlugin plugin = new(CSharpApiGeneratorOptions.FromSource(new EmptySource()) with
        {
            IndexTitle = [.. "ReactiveUI API"u8],
            IndexOrder = 3,
        });
        BuildDiscoverContext ctx = new((DirectoryPath)"/tmp", (DirectoryPath)"/out", [], new());

        try
        {
            await plugin.DiscoverAsync(ctx, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // EmptySource yields no TFM groups; the index entry is pre-seeded before generation, so it's still there.
        }

        await Assert.That(plugin.SyntheticNavEntries.Count).IsEqualTo(1);
        var entry = plugin.SyntheticNavEntries[0];
        await Assert.That(entry.RelativePath.Value).IsEqualTo("api/index.md");
        await Assert.That(Encoding.UTF8.GetString(entry.Title!)).IsEqualTo("ReactiveUI API");
        await Assert.That(entry.Order).IsEqualTo(3);
        await Assert.That(entry.Hidden).IsFalse();
    }

    /// <summary>When the index page is disabled, no nav entry is published.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoNavEntryWhenIndexPageDisabled()
    {
        CSharpApiGeneratorPlugin plugin = new(CSharpApiGeneratorOptions.FromSource(new EmptySource()) with { EmitIndexPage = false });
        BuildDiscoverContext ctx = new((DirectoryPath)"/tmp", (DirectoryPath)"/out", [], new());

        try
        {
            await plugin.DiscoverAsync(ctx, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // EmptySource yields no TFM groups; the check below is about the disabled-index behaviour, not generation.
        }

        await Assert.That(plugin.SyntheticNavEntries.Count).IsEqualTo(0);
    }

    /// <summary>Direct mode never reaches the nav-entry publication, so the list stays empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectModeHasNoNavEntries()
    {
        CSharpApiGeneratorPlugin plugin = new(CSharpApiGeneratorOptions.FromSource(new EmptySource()) with { Mode = CSharpApiGeneratorMode.Direct });
        BuildDiscoverContext ctx = new((DirectoryPath)"/tmp", (DirectoryPath)"/out", [], new());

        try
        {
            await plugin.DiscoverAsync(ctx, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // EmptySource yields no TFM groups; Direct mode surfaces that synchronously. Irrelevant to the nav-entry check.
        }

        await Assert.That(plugin.SyntheticNavEntries.Count).IsEqualTo(0);
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
