// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Common;

/// <summary>
/// Disposable rent-or-stack UTF-8 byte buffer for converting strings
/// to byte spans without per-call <c>Encoding.UTF8.GetBytes</c>
/// allocations.
/// </summary>
/// <remarks>
/// Pattern:
/// <code>
/// using var buf = new Utf8StackBuffer(url, stackalloc byte[Utf8StackBuffer.StackSize]);
/// return SomeByteApi(buf.Bytes);
/// </code>
/// Inputs whose UTF-8 encoding fits in the caller's stack span land
/// there; longer inputs rent from <see cref="ArrayPool{T}.Shared"/> and
/// the <see cref="Dispose"/> call returns the rental. The shape lets
/// every byte-first API expose a thin <c>string</c> adapter that
/// allocates nothing for sub-stack-budget URLs and a single pooled
/// rental for the rest.
/// </remarks>
public ref struct Utf8StackBuffer
{
    /// <summary>Pooled rental, or null when the data fits on the caller's stack.</summary>
    private byte[]? _rented;

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8StackBuffer"/>
    /// struct, encoding <paramref name="value"/> into
    /// <paramref name="stackBuffer"/> when it fits, or renting from the
    /// pool when it doesn't.
    /// </summary>
    /// <param name="value">Source string.</param>
    /// <param name="stackBuffer">Caller-supplied stack span (typically <c>stackalloc byte[Utf8StackBuffer.StackSize]</c>).</param>
    public Utf8StackBuffer(string value, Span<byte> stackBuffer)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount <= stackBuffer.Length)
        {
            var written = Encoding.UTF8.GetBytes(value, stackBuffer);
            Bytes = stackBuffer[..written];
            _rented = null;
            return;
        }

        _rented = ArrayPool<byte>.Shared.Rent(byteCount);
        var rentedWritten = Encoding.UTF8.GetBytes(value, _rented);
        Bytes = _rented.AsSpan(0, rentedWritten);
    }

    /// <summary>Gets the recommended stack-buffer size for typical URL / host adapters; covers ~99% of real URL lengths.</summary>
    public static int StackSize { get; } = 1024;

    /// <summary>Gets the encoded UTF-8 bytes (valid until <see cref="Dispose"/>).</summary>
    public ReadOnlySpan<byte> Bytes { get; }

    /// <summary>Returns the pooled rental (no-op when the data fit on the stack).</summary>
    public void Dispose()
    {
        if (_rented is null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_rented);
        _rented = null;
    }
}
