// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Contract for a config-file reader that produces a
/// <see cref="MkDocsConfig"/> from on-disk source bytes.
/// </summary>
/// <remarks>
/// One implementation per source format ships in its own assembly:
/// <c>NuStreamDocs.Config.MkDocs</c> handles <c>mkdocs.yml</c>,
/// <c>NuStreamDocs.Config.Zensical</c> handles Zensical's TOML.
/// Implementations are registered with <see cref="Building.DocBuilder"/>
/// through their assembly's <c>Use{Format}Config(...)</c> extension
/// method so the core library never takes the format-specific
/// dependency.
/// <para>
/// The <see cref="ReadAsync(System.IO.Stream, System.Threading.CancellationToken)"/>
/// default implementation rents a pooled <c>byte[]</c> sized to the
/// stream length where available and delegates to the span overload;
/// implementations that can stream-parse override it.
/// </para>
/// </remarks>
public interface IConfigReader
{
    /// <summary>Gets the human-readable format name (used in build logs).</summary>
    string FormatName { get; }

    /// <summary>Returns true when this reader can handle a file with <paramref name="extension"/>.</summary>
    /// <param name="extension">File extension including the leading dot, lowercase (e.g. <c>.yml</c>).</param>
    /// <returns>True when the reader recognizes the extension.</returns>
    bool RecognizesExtension(ReadOnlySpan<char> extension);

    /// <summary>Parses the file contents into a <see cref="MkDocsConfig"/>.</summary>
    /// <param name="utf8Source">UTF-8 file bytes.</param>
    /// <returns>The parsed config.</returns>
    MkDocsConfig Read(ReadOnlySpan<byte> utf8Source);

    /// <summary>
    /// Reads <paramref name="utf8Stream"/> into a pooled buffer and parses it.
    /// </summary>
    /// <param name="utf8Stream">UTF-8 source stream; positioned at the start of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed config.</returns>
    /// <remarks>
    /// Default behavior materializes the stream into a pooled
    /// <c>byte[]</c> and forwards to <see cref="Read(System.ReadOnlySpan{byte})"/>.
    /// Override on implementations that can parse incrementally.
    /// </remarks>
    async Task<MkDocsConfig> ReadAsync(Stream utf8Stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8Stream);

        var sizeHint = TryGetSizeHint(utf8Stream);
        var pool = ArrayPool<byte>.Shared;
        var buffer = pool.Rent(sizeHint);
        try
        {
            var written = 0;
            while (true)
            {
                var read = await utf8Stream.ReadAsync(buffer.AsMemory(written, buffer.Length - written), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                written += read;
                if (written == buffer.Length)
                {
                    var bigger = pool.Rent(buffer.Length * 2);
                    buffer.AsSpan(0, written).CopyTo(bigger);
                    pool.Return(buffer, clearArray: true);
                    buffer = bigger;
                }
            }

            return Read(buffer.AsSpan(0, written));
        }
        finally
        {
            pool.Return(buffer, clearArray: true);
        }
    }

    /// <summary>Returns a conservative initial buffer size for <paramref name="stream"/>.</summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>Buffer-size hint in bytes; 8 KiB when the length is unknown.</returns>
    private static int TryGetSizeHint(Stream stream)
    {
        const int DefaultSize = 8 * 1024;
        const int Cap = 1 * 1024 * 1024;
        try
        {
            if (stream.CanSeek && stream.Length is > 0 and <= Cap)
            {
                return checked((int)stream.Length);
            }
        }
        catch (NotSupportedException)
        {
            // Streams that lie about CanSeek; fall through.
        }

        return DefaultSize;
    }
}
