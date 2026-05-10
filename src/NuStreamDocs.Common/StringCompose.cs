// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Common;

/// <summary>
/// Centralised <see cref="string.Create{TState}(int, TState, System.Buffers.SpanAction{char, TState})"/>
/// helpers for the common "constant fragment + variable fragment" composition shapes used at
/// API boundaries across the project.
/// </summary>
/// <remarks>
/// Every helper allocates exactly one <see cref="string"/> sized to the precomputed total length
/// and fills it in place — no <c>string.Concat</c> trees, no <c>$"..."</c> compiler-lowering
/// uncertainty, and no closure capture (every lambda is <c>static</c> and receives its inputs via
/// the state parameter).
/// </remarks>
public static class StringCompose
{
    /// <summary>Char count of <c>int.MinValue</c> rendered via <c>int.TryFormat</c> (sign + 10 digits).</summary>
    private const int Int32MinValueCharCount = 11;

    /// <summary>Decimal-base divisor used when measuring digit counts.</summary>
    private const int DecimalBase = 10;

    /// <summary>Allocates one string equal to <paramref name="a"/> + <paramref name="b"/>.</summary>
    /// <param name="a">First fragment.</param>
    /// <param name="b">Second fragment.</param>
    /// <returns>Composed string.</returns>
    public static string Concat(string a, string b)
    {
        return string.Create(a.Length + b.Length, (a, b), static (span, p) =>
        {
            p.a.AsSpan().CopyTo(span);
            p.b.AsSpan().CopyTo(span[p.a.Length..]);
        });
    }

    /// <summary>Allocates one string equal to <paramref name="a"/> + <paramref name="b"/> + <paramref name="c"/>.</summary>
    /// <param name="a">First fragment.</param>
    /// <param name="b">Second fragment.</param>
    /// <param name="c">Third fragment.</param>
    /// <returns>Composed string.</returns>
    public static string Concat(string a, string b, string c)
    {
        return string.Create(a.Length + b.Length + c.Length, (a, b, c), static (span, p) =>
        {
            p.a.AsSpan().CopyTo(span);
            var i = p.a.Length;
            p.b.AsSpan().CopyTo(span[i..]);
            i += p.b.Length;
            p.c.AsSpan().CopyTo(span[i..]);
        });
    }

    /// <summary>Allocates one string equal to <paramref name="a"/> + <paramref name="b"/> + <paramref name="c"/> + <paramref name="d"/>.</summary>
    /// <param name="a">First fragment.</param>
    /// <param name="b">Second fragment.</param>
    /// <param name="c">Third fragment.</param>
    /// <param name="d">Fourth fragment.</param>
    /// <returns>Composed string.</returns>
    public static string Concat(string a, string b, string c, string d)
    {
        return string.Create(a.Length + b.Length + c.Length + d.Length, (a, b, c, d), static (span, p) =>
        {
            p.a.AsSpan().CopyTo(span);
            var i = p.a.Length;
            p.b.AsSpan().CopyTo(span[i..]);
            i += p.b.Length;
            p.c.AsSpan().CopyTo(span[i..]);
            i += p.c.Length;
            p.d.AsSpan().CopyTo(span[i..]);
        });
    }

    /// <summary>Allocates one string equal to <paramref name="a"/> + <paramref name="b"/> + <paramref name="c"/> + <paramref name="d"/> + <paramref name="e"/>.</summary>
    /// <param name="a">First fragment.</param>
    /// <param name="b">Second fragment.</param>
    /// <param name="c">Third fragment.</param>
    /// <param name="d">Fourth fragment.</param>
    /// <param name="e">Fifth fragment.</param>
    /// <returns>Composed string.</returns>
    public static string Concat(string a, string b, string c, string d, string e)
    {
        return string.Create(a.Length + b.Length + c.Length + d.Length + e.Length, (a, b, c, d, e), static (span, p) =>
        {
            p.a.AsSpan().CopyTo(span);
            var i = p.a.Length;
            p.b.AsSpan().CopyTo(span[i..]);
            i += p.b.Length;
            p.c.AsSpan().CopyTo(span[i..]);
            i += p.c.Length;
            p.d.AsSpan().CopyTo(span[i..]);
            i += p.d.Length;
            p.e.AsSpan().CopyTo(span[i..]);
        });
    }

    /// <summary>Allocates one string equal to <paramref name="prefix"/> + decimal-formatted <paramref name="value"/>.</summary>
    /// <param name="prefix">Constant prefix.</param>
    /// <param name="value">Integer value to render.</param>
    /// <returns>Composed string.</returns>
    public static string ConcatInt(string prefix, int value)
    {
        return string.Create(prefix.Length + DecimalDigitCount(value), (prefix, value), static (span, p) =>
        {
            p.prefix.AsSpan().CopyTo(span);
            p.value.TryFormat(span[p.prefix.Length..], out _, default, CultureInfo.InvariantCulture);
        });
    }

    /// <summary>Allocates one string equal to <paramref name="prefix"/> + decimal-formatted <paramref name="value"/> + <paramref name="suffix"/>.</summary>
    /// <param name="prefix">Constant prefix.</param>
    /// <param name="value">Integer value to render.</param>
    /// <param name="suffix">Constant suffix.</param>
    /// <returns>Composed string.</returns>
    public static string ConcatInt(string prefix, int value, string suffix)
    {
        var digitCount = DecimalDigitCount(value);
        return string.Create(prefix.Length + digitCount + suffix.Length, (prefix, value, suffix), static (span, p) =>
        {
            p.prefix.AsSpan().CopyTo(span);
            var i = p.prefix.Length;
            p.value.TryFormat(span[i..], out var written, default, CultureInfo.InvariantCulture);
            i += written;
            p.suffix.AsSpan().CopyTo(span[i..]);
        });
    }

    /// <summary>Counts the decimal digits (including a leading sign for negatives) needed to render <paramref name="value"/>.</summary>
    /// <param name="value">Integer to inspect.</param>
    /// <returns>Number of chars <c>int.TryFormat</c> will write.</returns>
    public static int DecimalDigitCount(int value)
    {
        if (value == int.MinValue)
        {
            return Int32MinValueCharCount;
        }

        var sign = value < 0 ? 1 : 0;
        var n = value < 0 ? -value : value;
        if (n is 0)
        {
            return 1;
        }

        var d = 0;
        while (n > 0)
        {
            d++;
            n /= DecimalBase;
        }

        return sign + d;
    }
}
