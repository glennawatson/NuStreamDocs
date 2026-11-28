// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.I18n;

/// <summary>One resolved alternate locale link for a logical documentation page.</summary>
/// <param name="LocaleCode">BCP-47 locale tag (for example <c>"en"</c> or <c>"fr"</c>).</param>
/// <param name="DisplayName">UTF-8 display name.</param>
/// <param name="Href">UTF-8 absolute or root-relative URL for that locale's page.</param>
[System.Diagnostics.DebuggerDisplay("LocaleAlternateLink: {LocaleCode}")]
internal readonly record struct LocaleAlternateLink(
    string LocaleCode,
    byte[] DisplayName,
    byte[] Href);
