// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace NuStreamDocs.Optimize;

/// <summary>
/// Async file-to-file compression helper backed by the .NET 10
/// <see cref="GZipStream"/> / <see cref="BrotliStream"/> async APIs.
/// </summary>
/// <remarks>
/// Pre-.NET 10, the async overloads on <see cref="GZipStream"/> ran
/// the actual compress on the calling thread — fine for one-shot
/// CLI work, terrible for our parallel-page pipeline. The .NET 10
/// implementation pushes the compress onto the IO threadpool, so a
/// per-page <see cref="Stream.CopyToAsync(Stream, CancellationToken)"/>
/// no longer serializes behind the file write. Use the async path
/// everywhere; the bytes never round-trip through a managed buffer.
/// </remarks>
internal static class Compressor
{
    /// <summary>The <c>.gz</c> filename suffix.</summary>
    private const string GzipSuffix = ".gz";

    /// <summary>The <c>.br</c> filename suffix.</summary>
    private const string BrotliSuffix = ".br";

    /// <summary>Writes <paramref name="sourcePath"/> through gzip into <c>{sourcePath}.gz</c>.</summary>
    /// <param name="sourcePath">Absolute path to the source file.</param>
    /// <param name="level">Compression level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous write.</returns>
    public static async Task WriteGzipAsync(string sourcePath, CompressionLevel level, CancellationToken cancellationToken)
    {
        await using var src = File.OpenRead(sourcePath);
        await using var dst = File.Create(sourcePath + GzipSuffix);
        await using var gz = new GZipStream(dst, level, leaveOpen: true);
        await src.CopyToAsync(gz, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes <paramref name="sourcePath"/> through brotli into <c>{sourcePath}.br</c>.</summary>
    /// <param name="sourcePath">Absolute path to the source file.</param>
    /// <param name="level">Compression level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous write.</returns>
    public static async Task WriteBrotliAsync(string sourcePath, CompressionLevel level, CancellationToken cancellationToken)
    {
        await using var src = File.OpenRead(sourcePath);
        await using var dst = File.Create(sourcePath + BrotliSuffix);
        await using var br = new BrotliStream(dst, level, leaveOpen: true);
        await src.CopyToAsync(br, cancellationToken).ConfigureAwait(false);
    }
}
