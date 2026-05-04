// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Coverage for CSharpApiGeneratorPlugin Name + OnRender + OnFinalize, FromSource, CustomInput, builder extensions.</summary>
public class CSharpApiGeneratorPluginCoverageTests
{
    /// <summary>Name returns the registered string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        var plugin = new CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions.FromSource(new EmptySource()));
        await Assert.That(plugin.Name.SequenceEqual("csharp-apigenerator"u8)).IsTrue();
    }

    /// <summary>OnRenderPageAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderNoOp()
    {
        var plugin = new CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions.FromSource(new EmptySource()));
        await plugin.OnRenderPageAsync(new("p.md", default, new(8)), CancellationToken.None);
    }

    /// <summary>OnFinalizeAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnFinalizeNoOp()
    {
        var plugin = new CSharpApiGeneratorPlugin(CSharpApiGeneratorOptions.FromSource(new EmptySource()));
        await plugin.OnFinalizeAsync(new("/out"), CancellationToken.None);
    }

    /// <summary>CustomInput stores the supplied source.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomInputCtor()
    {
        var src = new EmptySource();
        var input = new CustomInput(src);
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
