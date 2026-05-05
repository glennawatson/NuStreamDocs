// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator;

/// <summary>Diagnostic severity for one link validation result.</summary>
public enum LinkSeverity
{
    /// <summary>Reportable in non-strict mode; doesn't fail the build.</summary>
    Warning,

    /// <summary>Reportable in strict mode; non-zero exit code.</summary>
    Error
}
