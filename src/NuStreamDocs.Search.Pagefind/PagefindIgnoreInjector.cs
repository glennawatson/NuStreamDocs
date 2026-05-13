// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Search.Pagefind.Logging;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>
/// Injects <c>data-pagefind-ignore</c> on the <c>body</c> tag of HTML files whose site-relative path begins with one of the supplied prefixes,
/// so the Pagefind CLI skips them when it indexes the site.
/// </summary>
public static class PagefindIgnoreInjector
{
    /// <summary>Gets the tag prefix to match in HTML.</summary>
    private static ReadOnlySpan<byte> BodyOpen => "<body"u8;

    /// <summary>Gets the attribute injected on matching body tags.</summary>
    private static ReadOnlySpan<byte> IgnoreAttribute => " data-pagefind-ignore"u8;

    /// <summary>Gets the sentinel substring used to detect "already injected".</summary>
    private static ReadOnlySpan<byte> IgnoreSentinel => "data-pagefind-ignore"u8;

    /// <summary>Walks <paramref name="siteRoot"/> and injects the ignore attribute on every matching HTML file.</summary>
    /// <param name="siteRoot">Absolute path to the rendered site.</param>
    /// <param name="excludePrefixes">UTF-8 site-relative path prefixes (e.g. <c>"api/"u8</c>) to mark as ignored.</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of files modified.</returns>
    public static async Task<int> InjectAsync(
        DirectoryPath siteRoot,
        byte[][] excludePrefixes,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(siteRoot.Value);

        if (excludePrefixes is null || excludePrefixes.Length == 0 || !Directory.Exists(siteRoot.Value))
        {
            return 0;
        }

        var rootLength = NormalizedRootLength(siteRoot.Value);
        var modified = 0;

        foreach (var file in Directory.EnumerateFiles(siteRoot.Value, "*.html", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!RelativePathMatches(file, rootLength, excludePrefixes))
            {
                continue;
            }

            if (await InjectFileAsync(file, cancellationToken).ConfigureAwait(false))
            {
                modified++;
            }
        }

        PagefindIgnoreInjectorLogging.LogInjected(logger, modified);
        return modified;
    }

    /// <summary>Tests whether <paramref name="relativePath"/> (any separator style) begins with any UTF-8 prefix.</summary>
    /// <param name="relativePath">Site-relative path.</param>
    /// <param name="excludePrefixes">Prefix list.</param>
    /// <returns>True on match.</returns>
    internal static bool MatchesPrefix(string relativePath, byte[][] excludePrefixes)
    {
        if (excludePrefixes is null || excludePrefixes.Length == 0)
        {
            return false;
        }

        var relativeBytes = Encoding.UTF8.GetBytes(relativePath.Replace('\\', '/'));
        for (var i = 0; i < excludePrefixes.Length; i++)
        {
            if (relativeBytes.AsSpan().StartsWith(excludePrefixes[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the rewritten bytes when <paramref name="input"/>'s body tag needs the ignore attribute injected.</summary>
    /// <param name="input">Source HTML bytes.</param>
    /// <param name="output">Rewritten bytes when the method returns true; otherwise the original array.</param>
    /// <returns>True when injection happened.</returns>
    internal static bool TryInject(byte[] input, out byte[] output)
    {
        var tagStart = FindBodyTag(input);
        if (tagStart < 0)
        {
            output = input;
            return false;
        }

        var tagEnd = input.AsSpan(tagStart).IndexOf((byte)'>');
        if (tagEnd < 0)
        {
            output = input;
            return false;
        }

        // Idempotency: if the body tag already carries the attribute (manual authoring or a
        // re-run on already-injected output) leave the file untouched.
        var tag = input.AsSpan(tagStart, tagEnd + 1);
        if (tag.IndexOf(IgnoreSentinel) >= 0)
        {
            output = input;
            return false;
        }

        // Insert just after the "<body" so attribute order stays predictable and we don't
        // disturb any author-supplied attributes that follow.
        var insertAt = tagStart + BodyOpen.Length;
        output = new byte[input.Length + IgnoreAttribute.Length];
        input.AsSpan(0, insertAt).CopyTo(output);
        IgnoreAttribute.CopyTo(output.AsSpan(insertAt));
        input.AsSpan(insertAt).CopyTo(output.AsSpan(insertAt + IgnoreAttribute.Length));
        return true;
    }

    /// <summary>Computes the slice index just past the site root (and its trailing separator).</summary>
    /// <param name="root">Site root path.</param>
    /// <returns>Index into a child path where the site-relative portion begins.</returns>
    private static int NormalizedRootLength(string root) =>
        root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
            ? root.Length
            : root.Length + 1;

    /// <summary>Tests whether <paramref name="absolutePath"/>'s site-relative form begins with any prefix.</summary>
    /// <param name="absolutePath">Absolute file path.</param>
    /// <param name="rootLength">Length of the site-root portion (including trailing separator).</param>
    /// <param name="excludePrefixes">Prefix list.</param>
    /// <returns>True on match.</returns>
    private static bool RelativePathMatches(string absolutePath, int rootLength, byte[][] excludePrefixes) =>
        absolutePath.Length > rootLength && MatchesPrefix(absolutePath[rootLength..], excludePrefixes);

    /// <summary>Reads <paramref name="path"/>, injects the ignore attribute on the first body tag, writes it back.</summary>
    /// <param name="path">Absolute file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the file was rewritten; false when no body tag was found or the attribute was already present.</returns>
    private static async Task<bool> InjectFileAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (!TryInject(bytes, out var output))
        {
            return false;
        }

        await File.WriteAllBytesAsync(path, output, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Finds the byte index of the first <c>&lt;body</c> tag whose next character is whitespace, <c>&gt;</c>, or <c>/</c>.</summary>
    /// <param name="input">Source HTML bytes.</param>
    /// <returns>Index of the <c>&lt;</c>, or -1 if no body tag is present.</returns>
    private static int FindBodyTag(ReadOnlySpan<byte> input)
    {
        var search = 0;
        while (search < input.Length)
        {
            var hit = input[search..].IndexOf(BodyOpen);
            if (hit < 0)
            {
                return -1;
            }

            var absolute = search + hit;
            var nextIndex = absolute + BodyOpen.Length;
            if (nextIndex >= input.Length)
            {
                return -1;
            }

            // Reject "<bodysomething" — the next byte must terminate the tag name.
            var next = input[nextIndex];
            if (next is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or (byte)'>' or (byte)'/')
            {
                return absolute;
            }

            search = nextIndex;
        }

        return -1;
    }
}
