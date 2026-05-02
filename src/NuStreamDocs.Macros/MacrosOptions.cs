// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Macros;

/// <summary>Configuration for <see cref="MacrosPlugin"/>.</summary>
/// <param name="Variables">UTF-8 name → UTF-8 value map; <c>{{ name }}</c> markers in the source resolve through this dictionary via the byte-keyed alternate lookup.</param>
/// <param name="EscapeHtml">HTML-escape resolved values before substitution. Default off — markdown escapes downstream.</param>
/// <param name="WarnOnMissing">Log unresolved <c>{{ name }}</c> markers at <c>Warning</c>; otherwise leave them in place.</param>
public sealed record MacrosOptions(
    Dictionary<byte[], byte[]> Variables,
    bool EscapeHtml,
    bool WarnOnMissing)
{
    /// <summary>Gets the default option set — empty variables map, no escaping, no warnings.</summary>
    public static MacrosOptions Default { get; } = new(
        Variables: new(ByteArrayComparer.Instance),
        EscapeHtml: false,
        WarnOnMissing: false);
}
