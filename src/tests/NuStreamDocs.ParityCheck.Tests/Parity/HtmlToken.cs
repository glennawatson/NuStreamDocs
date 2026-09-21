// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>One piece of an HTML document: a tag, a comment or a run of text.</summary>
/// <param name="IsTag">Whether the token is a tag or comment rather than text.</param>
/// <param name="Text">Canonical spelling used for comparison.</param>
/// <param name="Name">Lower-case tag name, <c>!--</c> for a comment, or empty for text.</param>
/// <param name="Closing">Whether the token is a closing tag.</param>
/// <param name="ClassValue">Value of the <c>class</c> attribute, or empty.</param>
/// <param name="HasAttributes">Whether the tag keeps any attribute after class and style are dropped.</param>
/// <param name="IdValue">Value of the <c>id</c> attribute, or empty.</param>
/// <param name="Raw">The source text of the token as written.</param>
[DebuggerDisplay("{Text}")]
internal sealed record HtmlToken(bool IsTag, string Text, string Name, bool Closing, string ClassValue, bool HasAttributes, string IdValue, string Raw)
{
    /// <summary>Tag name recorded for a comment.</summary>
    private const string CommentName = "!--";

    /// <summary>Creates a text token.</summary>
    /// <param name="canonical">Text with character references canonicalized.</param>
    /// <param name="raw">Text as written.</param>
    /// <returns>The token.</returns>
    internal static HtmlToken ForText(string canonical, string raw) => new(false, canonical, string.Empty, false, string.Empty, false, string.Empty, raw);

    /// <summary>Creates a comment token.</summary>
    /// <param name="raw">Comment as written.</param>
    /// <returns>The token.</returns>
    internal static HtmlToken ForComment(string raw) => new(true, raw, CommentName, false, string.Empty, false, string.Empty, raw);
}
