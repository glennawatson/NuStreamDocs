// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Pre-resolution shape checks for hrefs — flags structural malformations that point at a
/// path-construction bug rather than a missing page (e.g. <c>..//api/...</c> from a parent
/// segment fused with a leading slash).
/// </summary>
public static class MalformedLinkDetector
{
    /// <summary>Diagnostic message for the parent-then-double-slash pattern.</summary>
    private const string ParentDoubleSlashMessage =
        "Malformed href: '..//' indicates a parent-segment fused with a leading slash; the link will resolve to a nested self-referential URL.";

    /// <summary>Diagnostic message for double-slash inside a path.</summary>
    private const string DoubleSlashMessage =
        "Malformed href: contains '//' inside the path; this usually means two path fragments were concatenated without trimming the separator.";

    /// <summary>Gets the UTF-8 bytes of the scheme separator (<c>://</c>).</summary>
    private static ReadOnlySpan<byte> SchemeSeparator => "://"u8;

    /// <summary>Gets the UTF-8 bytes of the parent-then-double-slash sequence (<c>..//</c>).</summary>
    private static ReadOnlySpan<byte> ParentDoubleSlash => "..//"u8;

    /// <summary>Gets the UTF-8 bytes of the double-slash sequence (<c>//</c>).</summary>
    private static ReadOnlySpan<byte> DoubleSlash => "//"u8;

    /// <summary>
    /// Returns a diagnostic when <paramref name="href"/> is structurally malformed, or
    /// <see cref="DiagnosticMessage.None"/> when the shape looks well-formed.
    /// </summary>
    /// <param name="href">UTF-8 bytes of the raw href (no <c>#fragment</c> stripping required).</param>
    /// <returns>The diagnostic message, or <see cref="DiagnosticMessage.None"/> on a clean href.</returns>
    public static DiagnosticMessage Inspect(ReadOnlySpan<byte> href)
    {
        if (href.IsEmpty)
        {
            return DiagnosticMessage.None;
        }

        var path = StripScheme(href);
        if (path.IndexOf(ParentDoubleSlash) >= 0)
        {
            return ParentDoubleSlashMessage;
        }

        return path.IndexOf(DoubleSlash) >= 0 ? DoubleSlashMessage : DiagnosticMessage.None;
    }

    /// <summary>
    /// Strips a leading <c>scheme://host</c> from <paramref name="href"/> so the double-slash
    /// scan only inspects the path portion.
    /// </summary>
    /// <param name="href">UTF-8 href bytes.</param>
    /// <returns>The path slice; the input verbatim when no scheme is present.</returns>
    private static ReadOnlySpan<byte> StripScheme(ReadOnlySpan<byte> href)
    {
        var schemeEnd = href.IndexOf(SchemeSeparator);
        if (schemeEnd < 0)
        {
            return href;
        }

        var afterScheme = href[(schemeEnd + SchemeSeparator.Length)..];
        var firstSlash = afterScheme.IndexOf((byte)'/');
        return firstSlash < 0 ? default : afterScheme[firstSlash..];
    }
}
