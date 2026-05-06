// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Optimize.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Optimize;

/// <summary>
/// Plugin that emits precompressed sibling artifacts for every output
/// file whose extension is in the configured set.
/// </summary>
/// <remarks>
/// Runs once at <see cref="FinalizeAsync"/> after every page is written.
/// Files smaller than <see cref="OptimizeOptions.MinimumBytes"/> are
/// skipped — gzip overhead can grow tiny payloads instead of shrinking
/// them, and the runtime savings of pre-serving them are negligible.
/// Sibling outputs that are already at least as new as the source are
/// left alone, so watch-loop rebuilds only re-compress changed files.
/// </remarks>
public sealed class OptimizePlugin(OptimizeOptions options, ILogger logger) : IBuildFinalizePlugin
{
    /// <summary>The <c>.gz</c> filename suffix.</summary>
    private const string GzipSuffix = ".gz";

    /// <summary>The <c>.br</c> filename suffix.</summary>
    private const string BrotliSuffix = ".br";

    /// <summary>Configured options.</summary>
    private readonly OptimizeOptions _options = ValidateOptions(options);

    /// <summary>
    /// Lookup of compressible extensions; per-instance and small (~7 entries by default),
    /// so a plain <see cref="HashSet{T}"/> outperforms <c>HashSet</c> here — the freeze
    /// cost wouldn't repay itself at this volume.
    /// </summary>
    private readonly HashSet<string> _extensionLookup =
        (options ?? throw new ArgumentNullException(nameof(options))).Extensions.ToStringSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Logger captured at construction; defaults to <see cref="NullLogger.Instance"/> when no logger is supplied.</summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Initializes a new instance of the <see cref="OptimizePlugin"/> class with default options.</summary>
    public OptimizePlugin()
        : this(OptimizeOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OptimizePlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public OptimizePlugin(OptimizeOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "optimize"u8;

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => new(PluginBand.Latest, 1);

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        if (_options.Formats == OptimizeFormats.None || !Directory.Exists(context.OutputRoot))
        {
            return;
        }

        await CompressTreeAsync(context.OutputRoot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Walks <paramref name="outputRoot"/> and compresses every eligible file in parallel.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous walk.</returns>
    internal async Task CompressTreeAsync(DirectoryPath outputRoot, CancellationToken cancellationToken)
    {
        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.Parallelism
        };

        var eligible = EnumerateEligible(outputRoot);
        OptimizeLoggingHelper.LogOptimizeStart(_logger, eligible.Length, outputRoot.Value);

        var processed = 0;
        long bytesSaved = 0;
        await Parallel.ForEachAsync(
            eligible,
            parallelOptions,
            async (path, ct) =>
            {
                var saved = await CompressOneAsync(path, ct).ConfigureAwait(false);
                Interlocked.Increment(ref processed);
                Interlocked.Add(ref bytesSaved, saved);
            })
            .ConfigureAwait(false);

        OptimizeLoggingHelper.LogOptimizeComplete(_logger, processed, bytesSaved);
    }

    /// <summary>Validates and returns <paramref name="opts"/>.</summary>
    /// <param name="opts">Options to validate.</param>
    /// <returns>The validated options.</returns>
    private static OptimizeOptions ValidateOptions(OptimizeOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        opts.Validate();
        return opts;
    }

    /// <summary>Returns every file under <paramref name="root"/> whose extension is compressible and whose size meets the minimum.</summary>
    /// <param name="root">Output root.</param>
    /// <returns>Eligible absolute paths.</returns>
    private FilePath[] EnumerateEligible(DirectoryPath root)
    {
        List<FilePath> buffer = new(capacity: 256);
        foreach (var info in new DirectoryInfo(root.Value).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var path = info.FullName;
            if (path.EndsWith(GzipSuffix, StringComparison.OrdinalIgnoreCase) || path.EndsWith(BrotliSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext) || !_extensionLookup.Contains(ext))
            {
                OptimizeLoggingHelper.LogFileSkipped(_logger, path, "extension not in compressible set");
                continue;
            }

            if (info.Length < _options.MinimumBytes)
            {
                OptimizeLoggingHelper.LogFileSkipped(_logger, path, "below minimum size");
                continue;
            }

            buffer.Add((FilePath)path);
        }

        return [.. buffer];
    }

    /// <summary>Compresses one file into whichever sibling formats are configured, skipping siblings already up to date.</summary>
    /// <param name="path">Source file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total bytes saved across the configured formats this run.</returns>
    private async Task<long> CompressOneAsync(FilePath path, CancellationToken cancellationToken)
    {
        var sourceInfo = new FileInfo(path.Value);
        var originalBytes = sourceInfo.Length;
        var sourceTimeUtc = sourceInfo.LastWriteTimeUtc;
        long saved = 0;

        if ((_options.Formats & OptimizeFormats.Gzip) == OptimizeFormats.Gzip)
        {
            saved += await CompressIfStaleAsync(path, GzipSuffix, originalBytes, sourceTimeUtc, _options.GzipLevel, cancellationToken).ConfigureAwait(false);
        }

        if ((_options.Formats & OptimizeFormats.Brotli) == OptimizeFormats.Brotli)
        {
            saved += await CompressIfStaleAsync(path, BrotliSuffix, originalBytes, sourceTimeUtc, _options.BrotliLevel, cancellationToken).ConfigureAwait(false);
        }

        return saved;
    }

    /// <summary>Compresses <paramref name="source"/> into <c>{source}{suffix}</c> when the sibling is missing or older than the source.</summary>
    /// <param name="source">Source file.</param>
    /// <param name="suffix">Sibling suffix (<c>.gz</c> / <c>.br</c>); also selects the writer.</param>
    /// <param name="originalBytes">Source size in bytes.</param>
    /// <param name="sourceTimeUtc">Source last-write time (UTC).</param>
    /// <param name="level">Compression level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bytes saved by this sibling (original minus compressed); 0 when the sibling is already up to date.</returns>
    private async Task<long> CompressIfStaleAsync(
        FilePath source,
        string suffix,
        long originalBytes,
        DateTime sourceTimeUtc,
        CompressionLevel level,
        CancellationToken cancellationToken)
    {
        var destInfo = new FileInfo(source.Value + suffix);
        if (destInfo.Exists && destInfo.LastWriteTimeUtc >= sourceTimeUtc)
        {
            return 0;
        }

        if (suffix == GzipSuffix)
        {
            await Compressor.WriteGzipAsync(source, level, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await Compressor.WriteBrotliAsync(source, level, cancellationToken).ConfigureAwait(false);
        }

        destInfo.Refresh();
        var compressedBytes = destInfo.Length;
        OptimizeLoggingHelper.LogFileProcessed(_logger, source.Value, originalBytes, compressedBytes);
        return originalBytes - compressedBytes;
    }
}
