// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary><see cref="IAssemblySource"/> over a fixed set of pre-built local assemblies.</summary>
/// <param name="Tfm">Target framework moniker stamped onto every walked type.</param>
/// <param name="AssemblyPaths">Absolute paths to the <c>.dll</c> files to walk.</param>
/// <param name="FallbackSearchPaths">Additional directories scanned for transitive references.</param>
internal sealed record LocalAssemblySource(
    ApiCompatString Tfm,
    FilePath[] AssemblyPaths,
    DirectoryPath[] FallbackSearchPaths) : IAssemblySource
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<AssemblyGroup> DiscoverAsync() => DiscoverAsync(CancellationToken.None);

    /// <inheritdoc/>
    public async IAsyncEnumerable<AssemblyGroup> DiscoverAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fallback = BuildFallbackIndex();
        yield return new(Tfm, ToStringArray(AssemblyPaths), fallback);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Adds managed assemblies from a directory; the first directory wins on duplicate filenames.</summary>
    /// <param name="index">Destination index, keyed by filename.</param>
    /// <param name="directory">Directory to scan.</param>
    private static void AddDllsFromDirectory(Dictionary<string, string> index, in DirectoryPath directory)
    {
        if (!directory.Exists())
        {
            return;
        }

        var files = Directory.GetFiles(directory.Value, "*.dll", SearchOption.TopDirectoryOnly);
        for (var i = 0; i < files.Length; i++)
        {
            var file = new FilePath(files[i]);
            if (!HasManagedMetadata(file))
            {
                continue;
            }

            _ = index.TryAdd(file.FileName, file);
        }
    }

    /// <summary>Determines whether a local DLL can supply assembly metadata.</summary>
    /// <param name="path">Candidate assembly file.</param>
    /// <returns>True when the file contains a managed assembly manifest.</returns>
    private static bool HasManagedMetadata(in FilePath path)
    {
        using var stream = File.OpenRead(path);
        try
        {
            using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            return reader.HasMetadata && reader.GetMetadataReader().IsAssembly;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>Materializes a <see cref="FilePath"/> array as a plain <c>string[]</c>.</summary>
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

    /// <summary>Builds a filename to absolute-path index for the resolver fallback.</summary>
    /// <returns>A case-insensitive lookup keyed by filename.</returns>
    private Dictionary<string, string> BuildFallbackIndex()
    {
        Dictionary<string, string> index = [with(StringComparer.OrdinalIgnoreCase)];
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
}
