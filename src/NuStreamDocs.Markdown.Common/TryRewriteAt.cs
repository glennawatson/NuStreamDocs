// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Markdown.Common;

/// <summary>Per-cursor rewrite probe — returns true and the consumed byte count when a transformation fired at <paramref name="offset"/>.</summary>
/// <param name="source">UTF-8 markdown bytes.</param>
/// <param name="offset">Cursor offset into <paramref name="source"/>.</param>
/// <param name="writer">UTF-8 sink for the substitution.</param>
/// <param name="consumed">Number of input bytes the match covered.</param>
/// <returns>True when the probe rewrote the cursor.</returns>
public delegate bool TryRewriteAt(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed);
