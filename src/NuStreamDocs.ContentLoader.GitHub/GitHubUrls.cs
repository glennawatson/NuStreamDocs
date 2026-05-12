// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.GitHub;

/// <summary>Builds GitHub REST / raw-content URLs (byte-assembled, surfaced as <see cref="UrlPath"/>) and the request headers GitHub expects.</summary>
internal static class GitHubUrls
{
    /// <summary>Initial byte capacity for a URL builder.</summary>
    private const int UrlCapacity = 128;

    /// <summary>Gets the GitHub REST API base.</summary>
    private static ReadOnlySpan<byte> ApiBase => "https://api.github.com/repos/"u8;

    /// <summary>Gets the raw-content host base.</summary>
    private static ReadOnlySpan<byte> RawBase => "https://raw.githubusercontent.com/"u8;

    /// <summary>Gets the <c>User-Agent</c> value GitHub requires on API requests.</summary>
    private static ReadOnlySpan<byte> UserAgent => "NuStreamDocs-ContentLoader"u8;

    /// <summary>Builds the recursive git-tree API URL for the repo reference.</summary>
    /// <param name="repo">Repository reference.</param>
    /// <returns>The API URL.</returns>
    public static UrlPath TreeApiUrl(in GitHubRepoRef repo)
    {
        ArrayBufferWriter<byte> writer = new(UrlCapacity);
        writer.Write(ApiBase);
        AppendOwnerRepo(writer, in repo);
        writer.Write("/git/trees/"u8);
        writer.Write(repo.Reference);
        writer.Write("?recursive=1"u8);
        return ToUrl(writer);
    }

    /// <summary>Builds the releases API URL for the repo.</summary>
    /// <param name="repo">Repository reference (only owner and name are used).</param>
    /// <returns>The API URL.</returns>
    public static UrlPath ReleasesApiUrl(in GitHubRepoRef repo)
    {
        ArrayBufferWriter<byte> writer = new(UrlCapacity);
        writer.Write(ApiBase);
        AppendOwnerRepo(writer, in repo);
        writer.Write("/releases?per_page=100"u8);
        return ToUrl(writer);
    }

    /// <summary>Builds the raw-content URL for a path within the repo at its reference.</summary>
    /// <param name="repo">Repository reference.</param>
    /// <param name="path">Repository-relative file path bytes.</param>
    /// <returns>The raw-content URL.</returns>
    public static UrlPath RawFileUrl(in GitHubRepoRef repo, ReadOnlySpan<byte> path)
    {
        ArrayBufferWriter<byte> writer = new(UrlCapacity + path.Length);
        writer.Write(RawBase);
        AppendOwnerRepo(writer, in repo);
        writer.Write("/"u8);
        writer.Write(repo.Reference);
        writer.Write("/"u8);
        writer.Write(path);
        return ToUrl(writer);
    }

    /// <summary>Builds the request headers — the required <c>User-Agent</c>, plus a bearer <c>Authorization</c> when a token is supplied.</summary>
    /// <param name="token">Personal access token bytes; empty for unauthenticated requests.</param>
    /// <returns>Header name/value byte pairs.</returns>
    public static (byte[] Name, byte[] Value)[] Headers(byte[] token) =>
        token is [_, ..]
            ? [([.. "User-Agent"u8], [.. UserAgent]), ([.. "Authorization"u8], BearerToken(token))]
            : [([.. "User-Agent"u8], [.. UserAgent])];

    /// <summary>Appends <c>{owner}/{repo}</c> to the URL builder.</summary>
    /// <param name="writer">Destination.</param>
    /// <param name="repo">Repository reference.</param>
    private static void AppendOwnerRepo(ArrayBufferWriter<byte> writer, in GitHubRepoRef repo)
    {
        writer.Write(repo.Owner);
        writer.Write("/"u8);
        writer.Write(repo.Repo);
    }

    /// <summary>Materialises the assembled UTF-8 bytes as a <see cref="UrlPath"/>.</summary>
    /// <param name="writer">Builder holding the URL bytes.</param>
    /// <returns>The URL path.</returns>
    private static UrlPath ToUrl(ArrayBufferWriter<byte> writer) =>
        new(Encoding.UTF8.GetString(writer.WrittenSpan));

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
