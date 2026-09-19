// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Building;

/// <summary>Reserves each page output during discovery and warns when another page would replace it.</summary>
internal sealed class PageOutputRegistry
{
    /// <summary>Initial number of pages expected in a small documentation site.</summary>
    private const int InitialCapacity = 32;

    /// <summary>Source page reserved for each output path.</summary>
    private readonly Dictionary<FilePath, RegisteredPage> _pages;

    /// <summary>Initializes a new instance of the <see cref="PageOutputRegistry"/> class.</summary>
    /// <param name="outputRoot">Existing output directory whose filesystem determines path casing.</param>
    internal PageOutputRegistry(in DirectoryPath outputRoot) =>
        _pages = [with(InitialCapacity, new OutputPathComparer(FileSystemPathComparison.GetComparer(outputRoot)))];

    /// <summary>Accepts a publishable page only when its output has not been reserved.</summary>
    /// <param name="item">Page discovered before dispatch to a rendering worker.</param>
    /// <param name="shell">Build options and diagnostics.</param>
    /// <returns>Whether the page should enter the rendering pipeline.</returns>
    internal bool TryRegister(in PageWorkItem item, in BuildPhaseShell shell)
    {
        if (!shell.Options.IncludeDrafts && (item.Flags & PageFlags.Draft) != 0)
        {
            return false;
        }

        var path = item.RelativePath.AsSpan();
        var lastSeparator = path.LastIndexOfAny('/', '\\');
        var isIndex = path[(lastSeparator + 1)..].Equals("index.md", StringComparison.OrdinalIgnoreCase);
        var outputPath = BuildPipelinePageProcessor.OutputPathFor(shell.OutputRoot, item.RelativePath, shell.Options.UseDirectoryUrls);
        var collisionPath = isIndex ? OutputPathBuilder.ForDirectoryUrls(shell.OutputRoot, item.RelativePath) : outputPath;
        FilePath key = Path.GetFullPath(collisionPath);
        if (_pages.TryAdd(key, new(item.RelativePath, outputPath, item.InMemorySource is not null)))
        {
            return true;
        }

        var existing = _pages[key];
        if (item.InMemorySource is not null)
        {
            BuildPipelineLoggingHelper.LogGeneratedPageConflict(shell.Log, item.RelativePath, existing.RelativePath, existing.OutputPath);
            return false;
        }

        if (existing.IsGenerated)
        {
            BuildPipelineLoggingHelper.LogSourcePageIgnoredForGeneratedPage(shell.Log, item.RelativePath, existing.RelativePath, existing.OutputPath);
            return false;
        }

        BuildPipelineLoggingHelper.LogSourcePageConflict(shell.Log, item.RelativePath, existing.RelativePath, existing.OutputPath);
        return false;
    }

    /// <summary>The page whose content owns an output destination.</summary>
    /// <param name="RelativePath">Source-relative page name.</param>
    /// <param name="OutputPath">Selected output file.</param>
    /// <param name="IsGenerated">Whether a plugin supplied the page content.</param>
    private readonly record struct RegisteredPage(FilePath RelativePath, FilePath OutputPath, bool IsGenerated);

    /// <summary>Compares outputs using the cached filesystem casing policy.</summary>
    /// <param name="comparer">Comparison policy determined for this build's output directory.</param>
    private sealed class OutputPathComparer(StringComparer comparer) : IEqualityComparer<FilePath>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(FilePath x, FilePath y) => comparer.Equals(x.Value, y.Value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(FilePath obj) => comparer.GetHashCode(obj.Value);
    }
}
