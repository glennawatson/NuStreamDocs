// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown;

/// <summary>
/// CommonMark block-level construct identified by the scanner.
/// </summary>
/// <remarks>
/// Plain <see langword="int"/> backing — Sonar's default analyzer
/// rejects byte-backed enums even though the descriptors are kept
/// pooled.
/// </remarks>
public enum BlockKind
{
    /// <summary>Sentinel: scanner has not classified the line yet.</summary>
    None = 0,

    /// <summary>Blank line; closes open paragraphs and lazy continuations.</summary>
    Blank,

    /// <summary>ATX heading (<c>#</c> .. <c>######</c>).</summary>
    AtxHeading,

    /// <summary>Setext heading underline (<c>===</c> or <c>---</c>).</summary>
    SetextHeading,

    /// <summary>Thematic break (<c>---</c>, <c>***</c>, <c>___</c>).</summary>
    ThematicBreak,

    /// <summary>Indented (4-space) code block.</summary>
    IndentedCode,

    /// <summary>Fenced code block fence line (open or close marker).</summary>
    FencedCode,

    /// <summary>Line inside an open fenced code block.</summary>
    FencedCodeContent,

    /// <summary>Block quote (<c>&gt;</c>).</summary>
    BlockQuote,

    /// <summary>List item, ordered or bullet.</summary>
    ListItem,

    /// <summary>Default text container.</summary>
    Paragraph,

    /// <summary>Opening line of a CommonMark HTML block (Type 1 or Type 6).</summary>
    /// <remarks>Type 1 covers <c>&lt;pre&gt;</c> / <c>&lt;script&gt;</c> / <c>&lt;style&gt;</c> / <c>&lt;textarea&gt;</c>; Type 6 covers a fixed list of block-level tag names.</remarks>
    HtmlBlock,

    /// <summary>Continuation line inside an open HTML block; emitted verbatim by the renderer.</summary>
    HtmlBlockContent,
}
