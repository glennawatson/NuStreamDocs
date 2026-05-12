// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using NuStreamDocs.ContentLoader.GitHub;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Cost of assembling a GitHub REST / raw-content URL — byte fragments into an
/// <c>ArrayBufferWriter&lt;byte&gt;</c> then materialised as a <c>UrlPath</c>. Called once per repo
/// (the tree API) and once per pulled Markdown file (raw content), so it scales with the file count.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class GitHubUrlBenchmarks
{
    /// <summary>A representative repository reference.</summary>
    private static readonly GitHubRepoRef Repo = new([.. "acme"u8], [.. "widgets"u8], [.. "main"u8]);

    /// <summary>A representative deep blob path.</summary>
    private static readonly byte[] BlobPath = [.. "docs/guide/getting-started/installation.md"u8];

    /// <summary>Builds the recursive git-tree API URL.</summary>
    /// <returns>The URL length.</returns>
    [Benchmark]
    public int TreeApiUrl() => GitHubUrls.TreeApiUrl(in Repo).Value.Length;

    /// <summary>Builds a raw-content URL for a blob path.</summary>
    /// <returns>The URL length.</returns>
    [Benchmark]
    public int RawFileUrl() => GitHubUrls.RawFileUrl(in Repo, BlobPath).Value.Length;
}
