// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace NuStreamDocs.Optimise;

/// <summary>Configuration for <see cref="OptimisePlugin"/>.</summary>
/// <param name="Formats">Compression formats to emit.</param>
/// <param name="GzipLevel">Compression level for gzip; <see cref="CompressionLevel.SmallestSize"/> by default.</param>
/// <param name="BrotliLevel">Compression level for brotli; <see cref="CompressionLevel.SmallestSize"/> by default.</param>
/// <param name="Extensions">File extensions to compress (lowercase, leading dot, e.g. <c>.html</c>).</param>
/// <param name="MinimumBytes">Skip compression when the source is smaller than this many bytes.</param>
/// <param name="Parallelism">Maximum parallel compression workers.</param>
public sealed record OptimiseOptions(
    OptimiseFormats Formats,
    CompressionLevel GzipLevel,
    CompressionLevel BrotliLevel,
    string[] Extensions,
    int MinimumBytes,
    int Parallelism)
{
    /// <summary>Gets the default extension set worth precompressing — text-like assets that gain a lot from gzip/brotli.</summary>
    public static string[] DefaultExtensions { get; } = [".html", ".css", ".js", ".json", ".svg", ".xml", ".txt"];

    /// <summary>Gets the default option set — both formats, smallest-size, default extensions, 1KiB minimum, default parallelism.</summary>
    public static OptimiseOptions Default { get; } = new(
        OptimiseFormats.Both,
        CompressionLevel.SmallestSize,
        CompressionLevel.SmallestSize,
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
