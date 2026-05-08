// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Layouts;

/// <summary>Bytes plus the parsed token stream for a single layout template.</summary>
/// <param name="Bytes">UTF-8 template source.</param>
/// <param name="Tokens">Token stream produced by <see cref="LayoutScanner"/>.</param>
internal readonly record struct TemplateUnit(byte[] Bytes, List<LayoutToken> Tokens)
{
    /// <summary>Parses <paramref name="bytes"/> into a token stream.</summary>
    /// <param name="bytes">UTF-8 template bytes.</param>
    /// <returns>The parsed unit.</returns>
    public static TemplateUnit From(byte[] bytes)
    {
        List<LayoutToken> tokens = new(64);
        LayoutScanner.Scan(bytes, tokens);
        return new(bytes, tokens);
    }
}
