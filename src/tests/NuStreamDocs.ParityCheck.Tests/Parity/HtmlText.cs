// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Text-level canonicalization used when comparing HTML.</summary>
internal static class HtmlText
{
    /// <summary>Longest character reference, in characters after the ampersand, that is decoded.</summary>
    private const int MaximumReferenceLength = 12;

    /// <summary>Length of the <c>#x</c> prefix of a hexadecimal character reference.</summary>
    private const int HexadecimalPrefixLength = 2;

    /// <summary>Decodes character references and re-encodes only what HTML requires, so entity spelling is irrelevant.</summary>
    /// <param name="raw">Text or attribute value as written.</param>
    /// <param name="attribute">Whether the text is an attribute value, which also escapes double quotes.</param>
    /// <returns>The canonical text.</returns>
    internal static string Canonicalize(string raw, bool attribute)
    {
        var builder = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '&' && TryReadReference(raw, i, out var decoded, out var end))
            {
                AppendCharacter(builder, decoded, attribute);
                i = end;
                continue;
            }

            AppendCharacter(builder, raw[i].ToString(), attribute);
        }

        return builder.ToString();
    }

    /// <summary>Collapses runs of spaces and tabs to one space and drops them around line breaks; HTML renders them identically.</summary>
    /// <param name="text">Text to collapse.</param>
    /// <returns>The collapsed text.</returns>
    internal static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not (' ' or '\t'))
            {
                _ = builder.Append(text[i]);
                continue;
            }

            var end = SkipSpacesAndTabs(text, i);
            var nextToLineBreak = (i > 0 && text[i - 1] == '\n') || (end < text.Length && text[end] == '\n');
            if (!nextToLineBreak)
            {
                _ = builder.Append(' ');
            }

            i = end - 1;
        }

        return builder.ToString();
    }

    /// <summary>Finds the end of the run of spaces and tabs that starts at <paramref name="start"/>.</summary>
    /// <param name="text">Text to scan.</param>
    /// <param name="start">Index of the first space or tab.</param>
    /// <returns>The index after the run.</returns>
    private static int SkipSpacesAndTabs(string text, int start)
    {
        var end = start;
        while (end < text.Length && text[end] is ' ' or '\t')
        {
            end++;
        }

        return end;
    }

    /// <summary>Reads a character reference that starts at the ampersand at <paramref name="start"/>.</summary>
    /// <param name="raw">Text containing the reference.</param>
    /// <param name="start">Index of the ampersand.</param>
    /// <param name="decoded">The decoded text when the method returns <see langword="true"/>.</param>
    /// <param name="end">Index of the terminating semicolon.</param>
    /// <returns><see langword="true"/> when a recognized reference starts here.</returns>
    private static bool TryReadReference(string raw, int start, out string decoded, out int end)
    {
        decoded = string.Empty;
        end = raw.IndexOf(';', start + 1, Math.Min(MaximumReferenceLength, raw.Length - start - 1));
        return end > start + 1 && TryDecode(raw[(start + 1)..end], out decoded);
    }

    /// <summary>Appends a decoded character, escaping what HTML requires.</summary>
    /// <param name="builder">Destination.</param>
    /// <param name="decoded">Decoded text.</param>
    /// <param name="attribute">Whether the text belongs to an attribute value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendCharacter(StringBuilder builder, string decoded, bool attribute) =>
        _ = builder.Append(decoded switch
        {
            "&" => "&amp;",
            "<" => "&lt;",
            ">" => "&gt;",
            "\"" => attribute ? "&quot;" : "\"",
            _ => decoded,
        });

    /// <summary>Decodes a named, decimal or hexadecimal character reference.</summary>
    /// <param name="reference">Reference body without the ampersand and semicolon.</param>
    /// <param name="decoded">The decoded text when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the reference is not recognized.</returns>
    private static bool TryDecode(string reference, out string decoded)
    {
        decoded = string.Empty;
        if (reference[0] == '#')
        {
            return TryDecodeNumeric(reference, out decoded);
        }

        var result = WebUtility.HtmlDecode($"&{reference};");
        if (result == $"&{reference};")
        {
            return false;
        }

        decoded = result;
        return true;
    }

    /// <summary>Decodes a <c>#123</c> or <c>#x7B</c> reference body.</summary>
    /// <param name="reference">Reference body starting with the number sign.</param>
    /// <param name="decoded">The decoded text when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the number is not a valid code point.</returns>
    private static bool TryDecodeNumeric(string reference, out string decoded)
    {
        decoded = string.Empty;
        var hexadecimal = reference.Length > 1 && reference[1] is 'x' or 'X';
        var digits = hexadecimal ? reference[HexadecimalPrefixLength..] : reference[1..];
        var style = hexadecimal ? NumberStyles.HexNumber : NumberStyles.None;
        if (!int.TryParse(digits, style, null, out var code) || !Rune.IsValid(code))
        {
            return false;
        }

        decoded = char.ConvertFromUtf32(code);
        return true;
    }
}
