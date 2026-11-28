// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Images;

/// <summary>Intrinsic pixel width and height read from an image header.</summary>
/// <param name="Width">Positive pixel width.</param>
/// <param name="Height">Positive pixel height.</param>
[System.Diagnostics.DebuggerDisplay("ImageDimensions: {Width}x{Height}")]
public readonly record struct ImageDimensions(int Width, int Height);
