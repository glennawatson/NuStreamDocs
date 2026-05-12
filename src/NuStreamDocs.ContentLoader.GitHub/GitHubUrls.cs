// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.ContentLoader.GitHub;

/// <summary>Builds GitHub REST / raw-content URLs and the request headers GitHub expects.</summary>
internal static class GitHubUrls
{
    /// <summary>Base of the GitHub REST API.</summary>
    private const string ApiBase = "https://api.github.com/repos/";

    /// <summary>Base of the raw-content host.</summary>
    private const string RawBase = "https://raw.githubusercontent.com/";

    /// <summary>Gets the <c>User-Agent</c> value GitHub requires on API requests.</summary>
    private static ReadOnlySpan<byte> UserAgent => "NuStreamDocs-ContentLoader"u8;

    /// <summary>Builds the recursive git-tree API URL for the repo reference.</summary>
    /// <param name="repo">Repository reference.</param>
    /// <returns>The API URL.</returns>
    public static string TreeApiUrl(in GitHubRepoRef repo)
    {
        StringBuilder builder = new(ApiBase);
        builder.Append(Text(repo.Owner)).Append('/').Append(Text(repo.Repo))
            .Append("/git/trees/").Append(Text(repo.Reference)).Append("?recursive=1");
        return builder.ToString();
    }

    /// <summary>Builds the releases API URL for the repo.</summary>
    /// <param name="repo">Repository reference (only owner and name are used).</param>
    /// <returns>The API URL.</returns>
    public static string ReleasesApiUrl(in GitHubRepoRef repo)
    {
        StringBuilder builder = new(ApiBase);
        builder.Append(Text(repo.Owner)).Append('/').Append(Text(repo.Repo)).Append("/releases?per_page=100");
        return builder.ToString();
    }

    /// <summary>Builds the raw-content URL for a path within the repo at its reference.</summary>
    /// <param name="repo">Repository reference.</param>
    /// <param name="path">Repository-relative file path.</param>
    /// <returns>The raw-content URL.</returns>
    public static string RawFileUrl(in GitHubRepoRef repo, string path)
    {
        StringBuilder builder = new(RawBase);
        builder.Append(Text(repo.Owner)).Append('/').Append(Text(repo.Repo)).Append('/')
            .Append(Text(repo.Reference)).Append('/').Append(path);
        return builder.ToString();
    }

    /// <summary>Builds the request headers — the required <c>User-Agent</c>, plus a bearer <c>Authorization</c> when a token is supplied.</summary>
    /// <param name="token">Personal access token bytes; empty for unauthenticated requests.</param>
    /// <returns>Header name/value byte pairs.</returns>
    public static (byte[] Name, byte[] Value)[] Headers(byte[] token) =>
        token is [_, ..]
            ? [([.. "User-Agent"u8], [.. UserAgent]), ([.. "Authorization"u8], BearerToken(token))]
            : [([.. "User-Agent"u8], [.. UserAgent])];

    /// <summary>Decodes a UTF-8 byte array to text for URL assembly.</summary>
    /// <param name="bytes">UTF-8 bytes.</param>
    /// <returns>The decoded text.</returns>
    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>Builds the <c>Bearer {token}</c> header value.</summary>
    /// <param name="token">Token bytes.</param>
    /// <returns>The header value bytes.</returns>
    private static byte[] BearerToken(byte[] token)
    {
        var prefix = "Bearer "u8;
        var value = new byte[prefix.Length + token.Length];
        prefix.CopyTo(value);
        token.CopyTo(value.AsSpan(prefix.Length));
        return value;
    }
}
