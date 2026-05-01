// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>Configuration for <see cref="HighlightPlugin"/>.</summary>
/// <param name="ExtraLexers">Caller-supplied lexers registered alongside the built-ins.</param>
public sealed record HighlightOptions(Lexer[] ExtraLexers)
{
    /// <summary>Gets the default option set — built-in lexers only.</summary>
    public static HighlightOptions Default { get; } = new([]);
}
