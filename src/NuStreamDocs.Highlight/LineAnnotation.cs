// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>Metadata extracted from trailing inline comment markers on a single source code line.</summary>
/// <param name="Kind">Diff or focus line classification.</param>
/// <param name="CalloutNumber">Positive callout badge number (<c>(1)!</c>), or <c>0</c> when none.</param>
[System.Diagnostics.DebuggerDisplay("LineAnnotation: Kind={Kind}, Callout={CalloutNumber}")]
internal readonly record struct LineAnnotation(LineAnnotationKind Kind, int CalloutNumber);
