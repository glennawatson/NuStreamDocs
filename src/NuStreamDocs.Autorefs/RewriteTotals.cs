// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Autorefs;

/// <summary>Per-pass resolved / missing reference counters returned by <see cref="AutorefsRewriter"/>.</summary>
/// <param name="Resolved">Resolved-reference accumulator.</param>
/// <param name="Missing">Unresolved-reference accumulator.</param>
public record struct RewriteTotals(int Resolved, int Missing);
