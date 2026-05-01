// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator;

/// <summary>One link-validation finding.</summary>
/// <param name="SourcePage">Page-relative URL the link came from.</param>
/// <param name="Link">The raw href value (or fragment).</param>
/// <param name="Severity">Whether the finding is fatal under strict mode.</param>
/// <param name="Message">Human-readable description.</param>
public readonly record struct LinkDiagnostic(string SourcePage, string Link, LinkSeverity Severity, string Message);
