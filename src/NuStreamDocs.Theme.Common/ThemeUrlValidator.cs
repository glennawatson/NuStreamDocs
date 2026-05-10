// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Configure-time validator for theme URL options (<c>SiteUrl</c>, <c>RepoUrl</c>, <c>EditUri</c>).
/// Catches malformed values that would otherwise leak into every emitted page's
/// canonical URL, edit link, or repo footer — so the build fails fast instead of producing a
/// site full of bad references.
/// </summary>
public static class ThemeUrlValidator
{
    /// <summary>Gets the UTF-8 bytes of the <c>http://</c> scheme prefix.</summary>
    private static ReadOnlySpan<byte> HttpScheme => "http://"u8;

    /// <summary>Gets the UTF-8 bytes of the <c>https://</c> scheme prefix.</summary>
    private static ReadOnlySpan<byte> HttpsScheme => "https://"u8;

    /// <summary>Gets the UTF-8 bytes of the protocol-relative scheme prefix.</summary>
    private static ReadOnlySpan<byte> ProtocolRelative => "//"u8;

    /// <summary>
    /// Inspects the supplied URL bytes and returns a diagnostic when the value is malformed,
    /// or null when the URL looks well-formed for its role.
    /// </summary>
    /// <param name="optionName">Friendly option name for the diagnostic (e.g. <c>SiteUrl</c>).</param>
    /// <param name="value">UTF-8 URL bytes; an empty value short-circuits to null.</param>
    /// <param name="requireAbsolute">When true, an empty or relative URL is flagged.</param>
    /// <returns>The diagnostic message, or <see cref="DiagnosticMessage.None"/> on a clean value.</returns>
    public static DiagnosticMessage Inspect(string optionName, ReadOnlySpan<byte> value, bool requireAbsolute)
    {
        if (value.IsEmpty)
        {
            return requireAbsolute
                ? (DiagnosticMessage)StringCompose.Concat(optionName, " is required but was not set; canonical URLs and Open Graph metadata will be omitted from every page.")
                : DiagnosticMessage.None;
        }

        if (!HasHttpScheme(value))
        {
            var valueText = Encoding.UTF8.GetString(value);

            // Protocol-relative is technically valid but ambiguous for canonicals, so warn.
            return value.StartsWith(ProtocolRelative)
                ? (DiagnosticMessage)StringCompose.Concat(optionName, " '", valueText, "' is protocol-relative; canonical URLs need an explicit http(s) scheme.")
                : (DiagnosticMessage)StringCompose.Concat(optionName, " '", valueText, "' is not an absolute http(s) URL; the build will emit invalid canonical / repo / edit links.");
        }

        if (HasFragmentOrQuery(value))
        {
            return (DiagnosticMessage)StringCompose.Concat(optionName, " '", Encoding.UTF8.GetString(value), "' contains a '?' or '#' segment; this leaks into every per-page canonical URL.");
        }

        return DiagnosticMessage.None;
    }

    /// <summary>True when <paramref name="value"/> starts with <c>http://</c> or <c>https://</c>.</summary>
    /// <param name="value">UTF-8 URL bytes.</param>
    /// <returns>True for absolute http(s) URLs.</returns>
    private static bool HasHttpScheme(ReadOnlySpan<byte> value) =>
        value.StartsWith(HttpsScheme) || value.StartsWith(HttpScheme);

    /// <summary>True when <paramref name="value"/> contains a <c>?</c> or <c>#</c>.</summary>
    /// <param name="value">UTF-8 URL bytes.</param>
    /// <returns>True for values carrying a query or fragment.</returns>
    private static bool HasFragmentOrQuery(ReadOnlySpan<byte> value) =>
        value.IndexOfAny((byte)'?', (byte)'#') >= 0;
}
