// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.I18n;

/// <summary>Declares one supported site locale and its human-readable label.</summary>
/// <param name="Code">BCP-47 locale tag (for example <c>"en"</c>, <c>"fr"</c>, or <c>"ja"</c>).</param>
/// <param name="DisplayName">UTF-8 display label shown in the language switcher.</param>
[System.Diagnostics.DebuggerDisplay("LocaleDefinition: {Code}")]
public readonly record struct LocaleDefinition(string Code, byte[] DisplayName);
