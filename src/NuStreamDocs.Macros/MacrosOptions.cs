// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Macros;

/// <summary>Configuration for <see cref="MacrosPlugin"/>.</summary>
/// <param name="Variables">UTF-8 name to UTF-8 value map used to resolve <c>{{ name }}</c> markers.</param>
/// <param name="EscapeHtml">When true, HTML-escapes resolved values before substitution.</param>
/// <param name="WarnOnMissing">When true, logs unresolved markers at <c>Warning</c>; otherwise they pass through unchanged.</param>
public sealed record MacrosOptions(
    Dictionary<byte[], byte[]> Variables,
    bool EscapeHtml,
    bool WarnOnMissing)
{
    /// <summary>Gets the default options: empty variables map, no escaping, no warnings.</summary>
    public static MacrosOptions Default { get; } = new(
        Variables: new(ByteArrayComparer.Instance),
        EscapeHtml: false,
        WarnOnMissing: false);
}
