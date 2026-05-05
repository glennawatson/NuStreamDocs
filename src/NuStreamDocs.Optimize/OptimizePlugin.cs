// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// </remarks>
public sealed class OptimizePlugin(OptimizeOptions options, ILogger logger) : IBuildFinalizePlugin
{
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
    internal async Task CompressTreeAsync(string outputRoot, CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.Parallelism,
        };

        var eligible = EnumerateEligible(outputRoot);
        OptimizeLoggingHelper.LogOptimizeStart(_logger, eligible.Length, outputRoot);

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
    private string[] EnumerateEligible(string root)
    {
        var buffer = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext) || !_extensionLookup.Contains(ext))
            {
                OptimizeLoggingHelper.LogFileSkipped(_logger, path, "extension not in compressible set");
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length < _options.MinimumBytes)
            {
                OptimizeLoggingHelper.LogFileSkipped(_logger, path, "below minimum size");
                continue;
            }

            buffer.Add(path);
        }

        return [.. buffer];
    }

    /// <summary>Compresses one file into whichever sibling formats are configured.</summary>
    /// <param name="path">Absolute path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total bytes saved across the configured formats (sum of original-minus-compressed for each emitted sibling).</returns>
    private async Task<long> CompressOneAsync(string path, CancellationToken cancellationToken)
    {
        var originalBytes = new FileInfo(path).Length;
        long saved = 0;
        if ((_options.Formats & OptimizeFormats.Gzip) == OptimizeFormats.Gzip)
        {
            await Compressor.WriteGzipAsync(path, _options.GzipLevel, cancellationToken).ConfigureAwait(false);
            var gzBytes = new FileInfo(path + ".gz").Length;
            saved += originalBytes - gzBytes;
            OptimizeLoggingHelper.LogFileProcessed(_logger, path, originalBytes, gzBytes);
        }

        if ((_options.Formats & OptimizeFormats.Brotli) != OptimizeFormats.Brotli)
        {
            return saved;
        }

        await Compressor.WriteBrotliAsync(path, _options.BrotliLevel, cancellationToken).ConfigureAwait(false);
        var brBytes = new FileInfo(path + ".br").Length;
        saved += originalBytes - brBytes;
        OptimizeLoggingHelper.LogFileProcessed(_logger, path, originalBytes, brBytes);
        return saved;
    }
}
