// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary><see cref="IAssemblySource"/> that concatenates the groups produced by each wrapped source.</summary>
/// <param name="sources">Sources to concatenate, walked in order.</param>
internal sealed class CompositeAssemblySource(IAssemblySource[] sources) : IAssemblySource, IDisposable
{
    /// <summary>Wrapped sources, walked in this order.</summary>
    private readonly IAssemblySource[] _sources = sources;

    /// <inheritdoc/>
    public IAsyncEnumerable<AssemblyGroup> DiscoverAsync() => DiscoverAsync(CancellationToken.None);

    /// <inheritdoc/>
    public async IAsyncEnumerable<AssemblyGroup> DiscoverAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < _sources.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await foreach (var group in _sources[i].DiscoverAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return group;
            }
        }
    }

    /// <summary>Disposes every wrapped source that implements <see cref="IDisposable"/>.</summary>
    public void Dispose()
    {
        for (var i = 0; i < _sources.Length; i++)
        {
            (_sources[i] as IDisposable)?.Dispose();
        }
    }
}
