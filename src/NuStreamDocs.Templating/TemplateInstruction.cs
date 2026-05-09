// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Templating;

/// <summary>
/// One instruction in a compiled <see cref="Template"/>. Offsets index into the original UTF-8
/// template source; <see cref="JumpTarget"/> points to the matching close (for SectionOpen /
/// InvertedSectionOpen) or open (for SectionClose), or -1.
/// </summary>
/// <param name="Op">Opcode.</param>
/// <param name="Start">Start offset of the literal slice or variable name.</param>
/// <param name="Length">Length in bytes of the literal slice or variable name.</param>
/// <param name="JumpTarget">Index of the matching open/close instruction, or -1.</param>
public readonly record struct TemplateInstruction(
    TemplateOp Op,
    int Start,
    int Length,
    int JumpTarget);
