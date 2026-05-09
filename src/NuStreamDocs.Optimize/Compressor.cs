// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using NuStreamDocs.Common;

namespace NuStreamDocs.Optimize;

/// <summary>Async file-to-file gzip / brotli compression.</summary>
internal static class Compressor
{
    /// <summary>The <c>.gz</c> filename suffix.</summary>
    private const string GzipSuffix = ".gz";

    /// <summary>The <c>.br</c> filename suffix.</summary>
    private const string BrotliSuffix = ".br";

    /// <summary>Writes <paramref name="sourcePath"/> through gzip into <c>{sourcePath}.gz</c>.</summary>
    /// <param name="sourcePath">Source file.</param>
    /// <param name="level">Compression level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous write.</returns>
    public static async Task WriteGzipAsync(FilePath sourcePath, CompressionLevel level, CancellationToken cancellationToken)
    {
        await using var src = File.OpenRead(sourcePath.Value);
        await using var dst = File.Create(sourcePath.Value + GzipSuffix);
        await using GZipStream gz = new(dst, level, leaveOpen: true);
        await src.CopyToAsync(gz, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes <paramref name="sourcePath"/> through brotli into <c>{sourcePath}.br</c>.</summary>
    /// <param name="sourcePath">Source file.</param>
    /// <param name="level">Compression level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous write.</returns>
    public static async Task WriteBrotliAsync(FilePath sourcePath, CompressionLevel level, CancellationToken cancellationToken)
    {
        await using var src = File.OpenRead(sourcePath.Value);
        await using var dst = File.Create(sourcePath.Value + BrotliSuffix);
        await using BrotliStream br = new(dst, level, leaveOpen: true);
        await src.CopyToAsync(br, cancellationToken).ConfigureAwait(false);
    }
}
