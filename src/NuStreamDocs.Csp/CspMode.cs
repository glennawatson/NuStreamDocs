// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Csp;

/// <summary>Whether the policy is enforced or only reported.</summary>
public enum CspMode
{
    /// <summary>Emit <c>&lt;meta http-equiv="Content-Security-Policy"&gt;</c> — violations are blocked.</summary>
    Enforce,

    /// <summary>Emit <c>&lt;meta http-equiv="Content-Security-Policy-Report-Only"&gt;</c> — violations are reported but not blocked (useful while rolling a policy out).</summary>
    ReportOnly,
}
