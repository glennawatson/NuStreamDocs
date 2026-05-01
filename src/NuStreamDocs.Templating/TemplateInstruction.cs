// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Templating;

/// <summary>
/// One instruction in a compiled <see cref="Template"/>.
/// </summary>
/// <remarks>
/// All offsets index into the original UTF-8 template source held by
/// the owning <see cref="Template"/>. <see cref="JumpTarget"/> points
/// to the matching close (for <see cref="TemplateOp.SectionOpen"/> /
/// <see cref="TemplateOp.InvertedSectionOpen"/>) or back to the
/// matching open (for <see cref="TemplateOp.SectionClose"/>) so the
/// renderer never re-scans the instruction stream.
/// </remarks>
/// <param name="Op">Opcode.</param>
/// <param name="Start">Start offset of the literal slice or variable name.</param>
/// <param name="Length">Length in bytes of the literal slice or variable name.</param>
/// <param name="JumpTarget">Index of the matching open/close instruction, or -1.</param>
public readonly record struct TemplateInstruction(
    TemplateOp Op,
    int Start,
    int Length,
    int JumpTarget);
