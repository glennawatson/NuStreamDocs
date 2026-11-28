// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.GitInfo;

/// <summary>Reads batched Git commit metadata for the input documentation directory.</summary>
public interface IGitLogProvider
{
    /// <summary>Returns raw UTF-8 <c>git log</c> output formatted with record (<c>0x1E</c>) and unit (<c>0x1F</c>) separators, or an empty array when unavailable.</summary>
    /// <param name="inputRoot">Input documentation directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTF-8 <c>git log</c> bytes.</returns>
    ValueTask<byte[]> ReadGitLogAsync(DirectoryPath inputRoot, CancellationToken cancellationToken);
}
