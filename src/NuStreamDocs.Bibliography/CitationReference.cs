// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography;

/// <summary>One <c>@key</c> + optional locator within a marker.</summary>
/// <param name="Key">Citation key (without the <c>@</c>).</param>
/// <param name="Locator">Pinpoint locator; <see cref="CitationLocator.None"/> when absent.</param>
public readonly record struct CitationReference(string Key, CitationLocator Locator);
