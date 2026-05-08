// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Layouts;

/// <summary>One token produced by <see cref="LayoutScanner"/>; offsets are template-relative.</summary>
/// <param name="Kind">Token kind.</param>
/// <param name="Start">Inclusive start offset of the token's source bytes.</param>
/// <param name="End">Exclusive end offset of the token's source bytes.</param>
/// <param name="PayloadStart">Inclusive start of the token's payload slice (the name / target / tag body).</param>
/// <param name="PayloadEnd">Exclusive end of the token's payload slice.</param>
internal readonly record struct LayoutToken(
    LayoutTokenKind Kind,
    int Start,
    int End,
    int PayloadStart,
    int PayloadEnd);
