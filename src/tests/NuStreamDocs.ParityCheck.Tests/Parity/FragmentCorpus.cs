// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Loads the Markdown fragment corpus from a directory.</summary>
internal static class FragmentCorpus
{
    /// <summary>File extension of a fragment.</summary>
    private const string FragmentExtension = ".md";

    /// <summary>File extension of a sidecar.</summary>
    private const string SidecarExtension = ".expect";

    /// <summary>Feature name of a fragment that sits directly in the corpus directory.</summary>
    private const string RootFeature = "root";

    /// <summary>File-name prefix of a fragment stored with LF line endings that is rendered with CRLF line endings.</summary>
    private const string CrlfPrefix = "crlf-";

    /// <summary>Loads every fragment below <paramref name="directory"/>, ordered by id.</summary>
    /// <param name="directory">Corpus directory.</param>
    /// <returns>The fragments with their sidecars.</returns>
    internal static Fragment[] Load(string directory)
    {
        var files = Directory.GetFiles(directory, $"*{FragmentExtension}", SearchOption.AllDirectories);
        var fragments = new Fragment[files.Length];
        for (var i = 0; i < files.Length; i++)
        {
            var id = Path.GetRelativePath(directory, files[i]).Replace('\\', '/')[..^FragmentExtension.Length];
            var slash = id.IndexOf('/', StringComparison.Ordinal);
            var sidecarPath = Path.ChangeExtension(files[i], SidecarExtension);
            fragments[i] = new(
                id,
                slash < 0 ? RootFeature : id[..slash],
                ReadMarkdown(files[i]),
                File.Exists(sidecarPath) ? Expectation.Parse(File.ReadAllText(sidecarPath, Encoding.UTF8)) : null);
        }

        Array.Sort(fragments, static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        return fragments;
    }

    /// <summary>Gets the path of the sidecar file for a fragment.</summary>
    /// <param name="directory">Corpus directory.</param>
    /// <param name="id">Fragment id.</param>
    /// <returns>The sidecar path, whether or not the file exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string SidecarPath(string directory, string id) => Path.Combine(directory, id + SidecarExtension);

    /// <summary>Reads the Markdown of a fragment file; a file whose name starts with <c>crlf-</c> has its line feeds turned into CRLF pairs.</summary>
    /// <param name="path">Fragment file path.</param>
    /// <returns>The Markdown source.</returns>
    private static string ReadMarkdown(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        return Path.GetFileName(path).StartsWith(CrlfPrefix, StringComparison.Ordinal)
            ? text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal)
            : text;
    }
}
