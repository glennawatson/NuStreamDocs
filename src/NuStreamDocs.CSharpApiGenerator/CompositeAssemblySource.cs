// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// <see cref="IAssemblySource"/> that concatenates the groups produced
/// by every wrapped source, in declaration order. Lets a caller mix
/// shapes — e.g. a NuGet manifest for third-party packages plus local
/// DLLs for in-development assemblies — without writing a custom
/// source.
/// </summary>
/// <remarks>
/// Each wrapped source is walked sequentially; cancellation is checked
/// between sources so a hung NuGet fetch on the second source still
/// honors <see cref="CancellationToken"/>.
/// </remarks>
/// <param name="sources">Sources to concatenate, walked in order.</param>
internal sealed class CompositeAssemblySource(IAssemblySource[] sources) : IAssemblySource, IDisposable
{
    /// <summary>Wrapped sources, walked in this order.</summary>
    private readonly IAssemblySource[] _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    /// <inheritdoc/>
    public IAsyncEnumerable<AssemblyGroup> DiscoverAsync() => DiscoverAsync(CancellationToken.None);

    /// <inheritdoc/>
    public async IAsyncEnumerable<AssemblyGroup> DiscoverAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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
    /// <remarks>
    /// Walked in declaration order so each inner source releases its own external resources
    /// (NuGet <see cref="HttpClient"/>, file handles, ...). Sources that don't implement
    /// <see cref="IDisposable"/> are left alone.
    /// </remarks>
    public void Dispose()
    {
        for (var i = 0; i < _sources.Length; i++)
        {
            (_sources[i] as IDisposable)?.Dispose();
        }
    }
}
