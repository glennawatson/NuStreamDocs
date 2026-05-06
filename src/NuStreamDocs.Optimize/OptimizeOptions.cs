// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace NuStreamDocs.Optimize;

/// <summary>Configuration for <see cref="OptimizePlugin"/>.</summary>
/// <remarks>
/// <see cref="Extensions"/> is stored as UTF-8 bytes per the project's byte-first pipeline rule.
/// String-shaped construction goes through <c>OptimizeOptionsExtensions</c>'s
/// <c>WithExtensions</c> / <c>AddExtensions</c> helpers, which encode once at the boundary.
/// </remarks>
/// <param name="Formats">Compression formats to emit.</param>
/// <param name="GzipLevel">Compression level for gzip.</param>
/// <param name="BrotliLevel">Compression level for brotli.</param>
/// <param name="Extensions">UTF-8 file-extension entries to compress (lowercase, leading dot, e.g. <c>.html</c>).</param>
/// <param name="MinimumBytes">Skip compression when the source is smaller than this many bytes.</param>
/// <param name="Parallelism">Maximum parallel compression workers.</param>
public sealed record OptimizeOptions(
    OptimizeFormats Formats,
    CompressionLevel GzipLevel,
    CompressionLevel BrotliLevel,
    byte[][] Extensions,
    int MinimumBytes,
    int Parallelism)
{
    /// <summary>Gets the default extension set worth precompressing — text-like assets that gain a lot from gzip/brotli.</summary>
    public static byte[][] DefaultExtensions { get; } =
    [
        [.. ".html"u8],
        [.. ".css"u8],
        [.. ".js"u8],
        [.. ".json"u8],
        [.. ".svg"u8],
        [.. ".xml"u8],
        [.. ".txt"u8]
    ];

    /// <summary>Gets the default option set — both formats, optimal level, default extensions, 1KiB minimum, default parallelism.</summary>
    public static OptimizeOptions Default { get; } = new(
        OptimizeFormats.Both,
        CompressionLevel.Optimal,
        CompressionLevel.Optimal,
        DefaultExtensions,
        MinimumBytes: 1024,
        Parallelism: Math.Max(1, Environment.ProcessorCount));

    /// <summary>Throws when any required field is invalid.</summary>
    /// <exception cref="ArgumentException">When <see cref="Extensions"/> is null/empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <see cref="Parallelism"/> is non-positive.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Extensions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Parallelism);
        ArgumentOutOfRangeException.ThrowIfNegative(MinimumBytes);
    }
}
