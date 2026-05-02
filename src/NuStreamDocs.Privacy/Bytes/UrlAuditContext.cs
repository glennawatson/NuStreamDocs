// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>Read-only context bundle passed down to URL byte audit walkers.</summary>
/// <param name="Filter">Host filter.</param>
/// <param name="Set">Concurrent set the matched URLs are added to (byte-array keyed).</param>
internal sealed record UrlAuditContext(HostFilter Filter, ConcurrentDictionary<byte[], byte> Set);
