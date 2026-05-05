// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Nav;

/// <summary>
/// Allocation-aware helpers for the nav-build hot path.
/// </summary>
/// <remarks>
/// <para>
/// On the rxui corpus the nav-build phase walks ~13,800 markdown files
/// across ~1,500 directories, and the recursive <c>BuildSection</c>
/// previously called <see cref="Path.GetRelativePath(string, string)"/>
/// + <see cref="string.Replace(char, char)"/> per file, paying a
/// two-string allocation tax even when the input was already under the
/// root. This helper folds both into one <see cref="string.Create{TState}(int, TState, System.Buffers.SpanAction{char, TState})"/>
/// when the fast-path applies (the file path starts with
/// <c>root + separator</c> — true for every output of
/// <see cref="Directory.GetFiles(string, string, SearchOption)"/>),
/// and falls back to the standard pair when it doesn't.
/// </para>
/// </remarks>
internal static class NavPathHelper
{
    /// <summary>Computes <paramref name="path"/>'s source-relative location under <paramref name="root"/> with forward-slash separators in a single allocation.</summary>
    /// <param name="root">Absolute docs root.</param>
    /// <param name="path">Absolute path under <paramref name="root"/> (file or directory).</param>
    /// <returns>The relative path, slash-normalized. Empty when <paramref name="path"/> equals <paramref name="root"/>.</returns>
    public static FilePath ToForwardSlashRelative(DirectoryPath root, FilePath path) =>
        ToForwardSlashRelativeCore(root.Value ?? string.Empty, path.Value ?? string.Empty);

    /// <summary>Computes <paramref name="directory"/>'s source-relative location under <paramref name="root"/> with forward-slash separators.</summary>
    /// <param name="root">Absolute docs root.</param>
    /// <param name="directory">Absolute directory under <paramref name="root"/>.</param>
    /// <returns>The relative directory path. Empty when <paramref name="directory"/> equals <paramref name="root"/>.</returns>
    public static DirectoryPath ToForwardSlashRelative(DirectoryPath root, DirectoryPath directory) =>
        ToForwardSlashRelativeCore(root.Value ?? string.Empty, directory.Value ?? string.Empty);

    /// <summary>Shared core that does the slash-normalized relative-path computation.</summary>
    /// <param name="rootStr">Absolute docs root.</param>
    /// <param name="pathStr">Absolute path under the root.</param>
    /// <returns>The relative path with forward slashes; empty when the inputs are equal.</returns>
    private static string ToForwardSlashRelativeCore(string rootStr, string pathStr)
    {
        if (TryFastPathSlice(rootStr, pathStr, out var sliceStart))
        {
            return CreateNormalized(pathStr, sliceStart);
        }

        // Fallback for cross-mount / weird casing — pay the two-string tax only here.
        return Path.GetRelativePath(rootStr, pathStr).Replace('\\', '/');
    }

    /// <summary>Tries the fast path — <paramref name="path"/> starts with <paramref name="root"/> + separator.</summary>
    /// <param name="root">Absolute docs root.</param>
    /// <param name="path">Candidate absolute path.</param>
    /// <param name="sliceStart">When <c>true</c>, the offset just past the trailing separator.</param>
    /// <returns>True when the fast path applies.</returns>
    private static bool TryFastPathSlice(string root, string path, out int sliceStart)
    {
        sliceStart = 0;

        // Equal paths -> empty relative.
        if (string.Equals(root, path, StringComparison.Ordinal))
        {
            sliceStart = path.Length;
            return true;
        }

        // Allow a trailing separator on the root we were handed (rare).
        var effectiveRootLen = root.Length;
        if (effectiveRootLen > 0 && IsSeparator(root[effectiveRootLen - 1]))
        {
            effectiveRootLen--;
        }

        if (path.Length <= effectiveRootLen)
        {
            return false;
        }

        if (!path.AsSpan(0, effectiveRootLen).SequenceEqual(root.AsSpan(0, effectiveRootLen)))
        {
            return false;
        }

        if (!IsSeparator(path[effectiveRootLen]))
        {
            return false;
        }

        sliceStart = effectiveRootLen + 1;
        return true;
    }

    /// <summary>Allocates the relative path with <c>\</c> rewritten to <c>/</c> via a single <see cref="string.Create{TState}(int, TState, System.Buffers.SpanAction{char, TState})"/> call.</summary>
    /// <param name="path">Original absolute path.</param>
    /// <param name="sliceStart">Offset just past the root + separator.</param>
    /// <returns>The relative path, slash-normalized.</returns>
    private static string CreateNormalized(string path, int sliceStart)
    {
        var length = path.Length - sliceStart;
        return length <= 0
            ? string.Empty
            : string.Create(length, (path, sliceStart), static (span, state) =>
        {
            var source = state.path.AsSpan(state.sliceStart);
            for (var i = 0; i < span.Length; i++)
            {
                var c = source[i];
                span[i] = c is '\\' ? '/' : c;
            }
        });
    }

    /// <summary>True when <paramref name="c"/> is either ASCII path separator.</summary>
    /// <param name="c">Character.</param>
    /// <returns>True when <paramref name="c"/> is <c>/</c> or <c>\</c>.</returns>
    private static bool IsSeparator(char c) => c is '/' or '\\';
}
