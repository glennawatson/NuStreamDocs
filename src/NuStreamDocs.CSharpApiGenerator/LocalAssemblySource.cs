// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// <see cref="IAssemblySource"/> over a fixed set of pre-built local
/// assemblies. Yields a single <see cref="AssemblyGroup"/>; the resolver
/// fallback index is built from <paramref name="FallbackSearchPaths"/>
/// plus the directories of the supplied <paramref name="AssemblyPaths"/>.
/// </summary>
/// <param name="Tfm">Target framework moniker stamped onto every walked type.</param>
/// <param name="AssemblyPaths">Absolute paths to the <c>.dll</c> files to walk.</param>
/// <param name="FallbackSearchPaths">Additional directories scanned for transitive references.</param>
internal sealed record LocalAssemblySource(string Tfm, string[] AssemblyPaths, string[] FallbackSearchPaths) : IAssemblySource
{
    /// <inheritdoc/>
    public IAsyncEnumerable<AssemblyGroup> DiscoverAsync() => DiscoverAsync(CancellationToken.None);

    /// <inheritdoc/>
    public async IAsyncEnumerable<AssemblyGroup> DiscoverAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fallback = BuildFallbackIndex();
        yield return new(Tfm, AssemblyPaths, fallback);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Builds a filename → absolute-path index covering every <c>.dll</c> next to the supplied assemblies plus every <see cref="FallbackSearchPaths"/> directory.</summary>
    /// <returns>A case-insensitive lookup keyed by filename.</returns>
    private Dictionary<string, string> BuildFallbackIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < AssemblyPaths.Length; i++)
        {
            var dir = Path.GetDirectoryName(AssemblyPaths[i]);
            if (!string.IsNullOrEmpty(dir))
            {
                AddDllsFromDirectory(index, dir);
            }
        }

        for (var i = 0; i < FallbackSearchPaths.Length; i++)
        {
            AddDllsFromDirectory(index, FallbackSearchPaths[i]);
        }

        return index;
    }

    /// <summary>Adds every <c>.dll</c> in <paramref name="directory"/> to <paramref name="index"/>; later entries are skipped on duplicate filenames so the first directory wins.</summary>
    /// <param name="index">Destination index.</param>
    /// <param name="directory">Directory to scan.</param>
    private static void AddDllsFromDirectory(Dictionary<string, string> index, string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var files = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
        for (var i = 0; i < files.Length; i++)
        {
            var name = Path.GetFileName(files[i]);
            if (!index.ContainsKey(name))
            {
                index[name] = files[i];
            }
        }
    }
}
