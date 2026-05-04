// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
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
internal sealed record LocalAssemblySource(
    ApiCompatString Tfm,
    FilePath[] AssemblyPaths,
    DirectoryPath[] FallbackSearchPaths) : IAssemblySource
{
    /// <inheritdoc/>
    public IAsyncEnumerable<AssemblyGroup> DiscoverAsync() => DiscoverAsync(CancellationToken.None);

    /// <inheritdoc/>
    public async IAsyncEnumerable<AssemblyGroup> DiscoverAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fallback = BuildFallbackIndex();
        yield return new(Tfm, ToStringArray(AssemblyPaths), fallback);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Builds a filename → absolute-path index covering every <c>.dll</c> next to the supplied assemblies plus every <see cref="FallbackSearchPaths"/> directory.</summary>
    /// <returns>A case-insensitive lookup keyed by filename.</returns>
    /// <remarks>The dictionary itself terminates at the SourceDocParser boundary (<see cref="AssemblyGroup.FallbackIndex"/>), so it stays string-shaped.</remarks>
    private Dictionary<string, string> BuildFallbackIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < AssemblyPaths.Length; i++)
        {
            var dir = AssemblyPaths[i].Directory;
            if (!dir.IsEmpty)
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
    /// <param name="index">Destination index, keyed by filename for SourceDocParser interop.</param>
    /// <param name="directory">Directory to scan.</param>
    private static void AddDllsFromDirectory(Dictionary<string, string> index, DirectoryPath directory)
    {
        if (!directory.Exists())
        {
            return;
        }

        // GetFiles over EnumerateFiles: we consume every entry into the dictionary, no short-circuit.
        var files = Directory.GetFiles(directory.Value, "*.dll", SearchOption.TopDirectoryOnly);
        for (var i = 0; i < files.Length; i++)
        {
            var name = Path.GetFileName(files[i]);
            if (!index.ContainsKey(name))
            {
                index[name] = files[i];
            }
        }
    }

    /// <summary>Materializes a <see cref="FilePath"/> array as a plain <c>string[]</c> at the SourceDocParser interop boundary.</summary>
    /// <param name="paths">Source paths.</param>
    /// <returns>String view; empty when input is empty.</returns>
    private static string[] ToStringArray(FilePath[] paths)
    {
        if (paths.Length is 0)
        {
            return [];
        }

        var result = new string[paths.Length];
        for (var i = 0; i < paths.Length; i++)
        {
            result[i] = paths[i].Value;
        }

        return result;
    }
}
