// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>One accessibility or performance finding raised against a rendered page.</summary>
/// <param name="Page">Site-relative URL of the page the finding came from.</param>
/// <param name="Rule">Which lint produced the finding.</param>
/// <param name="Message">Human-readable description carried across the diagnostic boundary.</param>
public readonly record struct AuditDiagnostic(UrlPath Page, AuditRule Rule, ApiCompatString Message);
