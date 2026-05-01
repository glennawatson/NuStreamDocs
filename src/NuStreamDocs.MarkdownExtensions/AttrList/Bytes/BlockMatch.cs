// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>Position bundle for a matched block-level attr-list element.</summary>
/// <param name="NameEnd">Offset just past the tag name.</param>
/// <param name="OpenGt">Offset of the opening <c>&gt;</c>.</param>
/// <param name="PrefixEnd">Offset just past the prefix text (start of trailing whitespace before <c>{:</c>).</param>
/// <param name="ContentStart">Offset of the inner attr-list text.</param>
/// <param name="ContentEnd">Offset just past the inner attr-list text.</param>
/// <param name="SuffixStart">Offset where the suffix text begins.</param>
/// <param name="CloseStart">Offset of <c>&lt;</c> in the closing tag.</param>
/// <param name="NameLen">Tag-name length.</param>
internal readonly record struct BlockMatch(int NameEnd, int OpenGt, int PrefixEnd, int ContentStart, int ContentEnd, int SuffixStart, int CloseStart, int NameLen);
