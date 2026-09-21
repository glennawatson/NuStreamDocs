// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The generated source of the pinned-output tests.</summary>
/// <param name="Text">Source text.</param>
/// <param name="RowCount">Number of <c>[Arguments]</c> rows in the source.</param>
[DebuggerDisplay("{RowCount} rows")]
internal sealed record PinnedRowSource(string Text, int RowCount);
