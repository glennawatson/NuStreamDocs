// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO.Hashing;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Thread-safe registry that maps external URLs to their target local
/// paths under the configured asset directory.
/// </summary>
/// <remarks>
/// Per-page <see cref="PrivacyPlugin"/> hooks register URLs as they
/// scan rendered HTML; the finalize pass enumerates the registry to
/// download every unique URL once.
/// </remarks>
internal sealed class ExternalAssetRegistry
{
    /// <summary>Length in bytes of the xxHash3 digest used to derive filenames.</summary>
    private const int HashByteLength = 8;

    /// <summary>Directory under the output root where externalized assets live.</summary>
    private readonly string _assetDirectory;

    /// <summary>Concurrent URL → local-relative-path map.</summary>
    private readonly ConcurrentDictionary<string, string> _urlToLocal = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ExternalAssetRegistry"/> class.</summary>
    /// <param name="assetDirectory">Forward-slash relative directory to write under (e.g. <c>assets/external</c>).</param>
    public ExternalAssetRegistry(string assetDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(assetDirectory);
        _assetDirectory = assetDirectory.TrimEnd('/');
    }

    /// <summary>Gets a snapshot of the registered <c>(url, localPath)</c> pairs.</summary>
    /// <returns>Right-sized snapshot array.</returns>
    public (string Url, string LocalPath)[] EntriesSnapshot()
    {
        KeyValuePair<string, string>[] snapshot = [.. _urlToLocal];
        var result = new (string Url, string LocalPath)[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            result[i] = (snapshot[i].Key, snapshot[i].Value);
        }

        return result;
    }

    /// <summary>Gets a snapshot of the registered URLs.</summary>
    /// <returns>Right-sized URL array.</returns>
    public string[] UrlsSnapshot()
    {
        var entries = EntriesSnapshot();
        var urls = new string[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            urls[i] = entries[i].Url;
        }

        return urls;
    }

    /// <summary>Returns the local path for <paramref name="url"/>, registering a new entry on first sight.</summary>
    /// <param name="url">External URL.</param>
    /// <returns>Forward-slash relative path under the output root (e.g. <c>assets/external/3a7f0d.png</c>).</returns>
    public string GetOrAdd(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        return _urlToLocal.GetOrAdd(url, static (key, dir) => BuildLocalPath(key, dir), _assetDirectory);
    }

    /// <summary>Computes the local path for <paramref name="url"/> from its xxHash3 digest and original extension.</summary>
    /// <param name="url">External URL.</param>
    /// <param name="assetDirectory">Forward-slash relative directory (no trailing slash).</param>
    /// <returns>Forward-slash relative path.</returns>
    private static string BuildLocalPath(string url, string assetDirectory)
    {
        Span<byte> digest = stackalloc byte[HashByteLength];
        XxHash3.Hash(System.Text.Encoding.UTF8.GetBytes(url), digest);
        var hex = Convert.ToHexStringLower(digest);
        var extension = ExtractExtension(url);
        return string.IsNullOrEmpty(extension)
            ? $"{assetDirectory}/{hex}"
            : $"{assetDirectory}/{hex}{extension}";
    }

    /// <summary>Pulls the file extension (including the leading dot) off the URL's path component.</summary>
    /// <param name="url">External URL.</param>
    /// <returns>Extension (with leading dot) or an empty string when none is recognizable.</returns>
    private static string ExtractExtension(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var path = uri.AbsolutePath;
        var dot = path.LastIndexOf('.');
        if (dot < 0 || dot >= path.Length - 1)
        {
            return string.Empty;
        }

        var ext = path[dot..];
        for (var i = 1; i < ext.Length; i++)
        {
            // Refuse extensions with non-alphanumeric chars — defends against
            // pathological URLs whose path ends with `.something/with/slashes`.
            var c = ext[i];
            if (!char.IsLetterOrDigit(c))
            {
                return string.Empty;
            }
        }

        return ext.Length <= 8 ? ext : string.Empty;
    }
}
