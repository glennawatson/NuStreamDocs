// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Locates a single quoted attribute by name within a tag-body span,
/// matching <c>name="value"</c> or <c>name='value'</c>
/// case-insensitively with whitespace allowed around the <c>=</c>.
/// </summary>
internal static class AnchorAttributeFinder
{
    /// <summary>Locates the first occurrence of the named quoted attribute in <paramref name="attrs"/>.</summary>
    /// <param name="attrs">Tag body span (between <c>&lt;a</c> and the closing <c>&gt;</c>).</param>
    /// <param name="name">Lowercase ASCII attribute name to match.</param>
    /// <returns>Range of the matched attribute, or <see cref="NamedAttribute.None"/> when no match exists.</returns>
    public static NamedAttribute Find(ReadOnlySpan<byte> attrs, ReadOnlySpan<byte> name)
    {
        for (var p = 0; p < attrs.Length; p++)
        {
            var match = TryMatchAt(attrs, p, name);
            if (match.Found)
            {
                return match;
            }
        }

        return NamedAttribute.None;
    }

    /// <summary>Tries to match the named attribute starting exactly at <paramref name="p"/>.</summary>
    /// <param name="attrs">Tag body span.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="name">Lowercase attribute name.</param>
    /// <returns>The match, or <see cref="NamedAttribute.None"/>.</returns>
    private static NamedAttribute TryMatchAt(ReadOnlySpan<byte> attrs, int p, ReadOnlySpan<byte> name)
    {
        if (!ByteHelpers.IsWordBoundary(attrs, p) || !ByteHelpers.StartsWithIgnoreAsciiCase(attrs, p, name))
        {
            return NamedAttribute.None;
        }

        var afterName = p + name.Length;
        var afterWs = ByteHelpers.SkipWhitespace(attrs, afterName);
        if (afterWs >= attrs.Length || attrs[afterWs] is not (byte)'=')
        {
            return NamedAttribute.None;
        }

        var afterEq = ByteHelpers.SkipWhitespace(attrs, afterWs + 1);
        if (afterEq >= attrs.Length || attrs[afterEq] is not ((byte)'"' or (byte)'\''))
        {
            return NamedAttribute.None;
        }

        var quote = attrs[afterEq];
        var valStart = afterEq + 1;
        var endQuoteRel = attrs[valStart..].IndexOf(quote);
        if (endQuoteRel < 0)
        {
            return NamedAttribute.None;
        }

        var valEnd = valStart + endQuoteRel;
        return new(p, valEnd + 1, valStart, valEnd);
    }
}
