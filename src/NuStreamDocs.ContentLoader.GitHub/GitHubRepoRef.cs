// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.ContentLoader.GitHub;

/// <summary>Identifies a point in a GitHub repository: owner, repository name, and a git reference (branch, tag, or commit SHA).</summary>
/// <param name="Owner">Repository owner (user or organization).</param>
/// <param name="Repo">Repository name.</param>
/// <param name="Reference">Branch name, tag name, or commit SHA.</param>
public readonly record struct GitHubRepoRef(byte[] Owner, byte[] Repo, byte[] Reference);
