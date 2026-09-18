// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Serve.Tests;

/// <summary>Aborts a request after a specified number of response writes.</summary>
/// <param name="request">Request cancellation source.</param>
/// <param name="abortAfterWrites">Number of successful writes before aborting.</param>
internal sealed class RequestAbortStream(CancellationTokenSource request, int abortAfterWrites) : Stream
{
    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Gets the number of writes completed before cancellation.</summary>
    internal int WriteCount { get; private set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        OnWriteAsync(cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(OnWriteAsync(cancellationToken));

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Observes the supplied token and aborts after the configured write.</summary>
    /// <param name="cancellationToken">Token supplied by the response writer.</param>
    /// <returns>Completion of the write or triggered request cancellation.</returns>
    private Task OnWriteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteCount++;
        return WriteCount == abortAfterWrites ? request.CancelAsync() : Task.CompletedTask;
    }
}
