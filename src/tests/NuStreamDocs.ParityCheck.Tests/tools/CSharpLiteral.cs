// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Formats text as a C# string literal.</summary>
internal static class CSharpLiteral
{
    /// <summary>Number of quote characters that surround a literal.</summary>
    private const int SurroundingQuotes = 2;

    /// <summary>Quotes <paramref name="value"/> as a regular string literal, escaping quotes, backslashes and control characters.</summary>
    /// <param name="value">Text to quote.</param>
    /// <returns>The literal including its surrounding quotes.</returns>
    internal static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + SurroundingQuotes);
        _ = builder.Append('"');
        foreach (var c in value)
        {
            _ = builder.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' or (>= '\u007f' and < ' ') => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        }

        return builder.Append('"').ToString();
    }
}
