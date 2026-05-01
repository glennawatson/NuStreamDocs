// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Blog.Common;

/// <summary>Shared slugification helper for tag/category archive filenames.</summary>
internal static class BlogSlugifier
{
    /// <summary>Difference between an ASCII uppercase and lowercase letter.</summary>
    private const int AsciiCaseShift = 32;

    /// <summary>Slugifies <paramref name="value"/>: lowercased ASCII alphanumerics plus <c>-</c>/<c>_</c>; everything else collapses or drops.</summary>
    /// <param name="value">Source text.</param>
    /// <param name="fallback">Returned when nothing slug-safe survives.</param>
    /// <returns>Slug.</returns>
    public static string Slugify(string value, string fallback)
    {
        var chars = new char[value.Length];
        var written = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var mapped = MapChar(value[i]);
            if (mapped != '\0')
            {
                chars[written++] = mapped;
            }
        }

        return written == 0 ? fallback : new(chars, 0, written);
    }

    /// <summary>Maps one character to its slug equivalent or <c>\0</c> when it should be dropped.</summary>
    /// <param name="c">Source character.</param>
    /// <returns>Slug char or NUL.</returns>
    private static char MapChar(char c)
    {
        if (IsLowerAlphanumeric(c) || c is '-' or '_')
        {
            return c;
        }

        if (c is >= 'A' and <= 'Z')
        {
            return (char)(c + AsciiCaseShift);
        }

        return c is ' ' or '/' ? '-' : '\0';
    }

    /// <summary>True for ASCII <c>a–z</c> or <c>0–9</c>.</summary>
    /// <param name="c">Char.</param>
    /// <returns>True when slug-safe without case translation.</returns>
    private static bool IsLowerAlphanumeric(char c) =>
        c is >= 'a' and <= 'z' or >= '0' and <= '9';
}
