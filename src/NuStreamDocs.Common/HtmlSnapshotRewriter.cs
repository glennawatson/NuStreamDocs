// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Common;

/// <summary>
/// Snapshot-then-callback helper for plugins that need to read the current
/// rendered HTML while writing a transformed copy back into the same
/// <see cref="ArrayBufferWriter{T}"/>. Pools the snapshot buffer through
/// <see cref="ArrayPool{T}.Shared"/> so the per-page hot path stays
/// allocation-free.
/// </summary>
public static class HtmlSnapshotRewriter
{
    /// <summary>Callback invoked with the read-only snapshot and the (now-empty) writer.</summary>
    /// <typeparam name="TState">User state passed through unchanged.</typeparam>
    /// <param name="snapshot">Snapshot of the bytes that were in <paramref name="writer"/>.</param>
    /// <param name="writer">Reset target buffer for the callback output.</param>
    /// <param name="state">User state.</param>
    public delegate void SnapshotRewrite<TState>(ReadOnlySpan<byte> snapshot, ArrayBufferWriter<byte> writer, TState state);

    /// <summary>
    /// Snapshots the current contents of <paramref name="html"/>, resets the writer,
    /// and invokes <paramref name="callback"/> with the snapshot + the now-empty writer.
    /// No-op when the writer is empty.
    /// </summary>
    /// <typeparam name="TState">User state type.</typeparam>
    /// <param name="html">HTML buffer to read from and callback into.</param>
    /// <param name="state">State forwarded to <paramref name="callback"/>.</param>
    /// <param name="callback">Callback that produces the new buffer contents.</param>
    public static void Rewrite<TState>(ArrayBufferWriter<byte> html, TState state, SnapshotRewrite<TState> callback)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(callback);

        var written = html.WrittenSpan;
        var length = written.Length;
        if (length is 0)
        {
            return;
        }

        var rental = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            written.CopyTo(rental);
            html.ResetWrittenCount();
            callback(rental.AsSpan(0, length), html, state);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rental);
        }
    }
}
