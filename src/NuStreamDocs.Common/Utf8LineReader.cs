// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Common;

/// <summary>
/// Async UTF-8 line-by-line reader over a <see cref="System.IO.Stream"/>.
/// </summary>
/// <remarks>
/// Used by the config readers so the whole file never has to live in
/// memory at once. The reader maintains a pooled buffer that grows on
/// demand to fit the longest line, slides completed lines off the
/// front, and exposes each line as a <see cref="ReadOnlyMemory{Byte}"/>
/// slice valid until the next call to <see cref="TryReadLineAsync"/>.
/// <para>
/// The yielded slice is over the internal buffer — copy or process it
/// before the next read advances. CR/LF and lone LF terminators are
/// both stripped from the returned slice.
/// </para>
/// </remarks>
public sealed class Utf8LineReader : IDisposable
{
    /// <summary>Carriage-return byte.</summary>
    private const byte Cr = (byte)'\r';

    /// <summary>Line-feed byte.</summary>
    private const byte Lf = (byte)'\n';

    /// <summary>Initial buffer capacity if the stream has no length hint.</summary>
    private const int DefaultBufferSize = 8 * 1024;

    /// <summary>Cap on the size hint we trust from a seekable stream.</summary>
    private const int MaxSeekableHint = 1 * 1024 * 1024;

    /// <summary>Source stream.</summary>
    private readonly Stream _stream;

    /// <summary>Whether the reader owns (and should dispose) <see cref="_stream"/>.</summary>
    private readonly bool _ownsStream;

    /// <summary>Pooled buffer holding unread bytes plus the most-recent yielded line.</summary>
    private byte[] _buffer;

    /// <summary>Offset of the first byte not yet returned.</summary>
    private int _start;

    /// <summary>Offset just past the last byte read from the stream.</summary>
    private int _end;

    /// <summary>True when the underlying stream has reported end-of-input.</summary>
    private bool _streamExhausted;

    /// <summary>Initializes a new instance of the <see cref="Utf8LineReader"/> class.</summary>
    /// <param name="stream">UTF-8 source stream.</param>
    /// <param name="leaveOpen">When false, dispose the stream on <see cref="Dispose"/>.</param>
    public Utf8LineReader(Stream stream, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _ownsStream = !leaveOpen;
        _buffer = ArrayPool<byte>.Shared.Rent(InitialCapacity(stream));
    }

    /// <summary>
    /// Reads the next line; returns false when the stream is exhausted.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple — <c>HasLine</c> is true when a line was read; <c>Line</c> holds the line bytes (no terminator).</returns>
    public async ValueTask<(bool HasLine, ReadOnlyMemory<byte> Line)> TryReadLineAsync(CancellationToken cancellationToken)
    {
        // Loop is bounded: each iteration either returns, or calls
        // FillAsync which is guaranteed to either grow the buffer or
        // mark the stream exhausted — so at most O(file-size /
        // chunk-size) trips before we hit one of the return paths.
        while (!_streamExhausted || _end > _start)
        {
            var unread = _buffer.AsMemory(_start, _end - _start);
            var lf = unread.Span.IndexOf(Lf);
            if (lf >= 0)
            {
                var lineLength = lf > 0 && unread.Span[lf - 1] == Cr ? lf - 1 : lf;
                var line = _buffer.AsMemory(_start, lineLength);
                _start += lf + 1;
                return (true, line);
            }

            if (_streamExhausted)
            {
                var tail = _buffer.AsMemory(_start, _end - _start);
                _start = _end;
                return (true, tail);
            }

            await FillAsync(cancellationToken).ConfigureAwait(false);
        }

        return (false, ReadOnlyMemory<byte>.Empty);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_buffer is { Length: > 0 })
        {
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
            _buffer = [];
        }

        if (!_ownsStream)
        {
            return;
        }

        _stream.Dispose();
    }

    /// <summary>Returns a reasonable initial buffer size for <paramref name="stream"/>.</summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>Buffer size hint.</returns>
    private static int InitialCapacity(Stream stream)
    {
        try
        {
            if (stream.CanSeek && stream.Length is > 0 and <= MaxSeekableHint)
            {
                return checked((int)stream.Length);
            }
        }
        catch (NotSupportedException)
        {
            // Some streams claim CanSeek but throw on Length.
        }

        return DefaultBufferSize;
    }

    /// <summary>Reads more bytes into the buffer, growing it if it's full.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the buffer has more bytes or the stream ends.</returns>
    private async ValueTask FillAsync(CancellationToken cancellationToken)
    {
        if (_start > 0)
        {
            // Slide unread bytes to the front so we have room to read.
            Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
            _end -= _start;
            _start = 0;
        }

        if (_end == _buffer.Length)
        {
            var bigger = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
            Buffer.BlockCopy(_buffer, 0, bigger, 0, _end);
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
            _buffer = bigger;
        }

        var read = await _stream.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            _streamExhausted = true;
            return;
        }

        _end += read;
    }
}
