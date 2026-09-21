// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>
/// A sidecar (<c>.expect</c>) file: a <c>kind:</c> line, a <c>reason:</c> line, then an optional <c>---</c> line
/// followed by the exact HTML the renderer is expected to produce.
/// </summary>
/// <param name="Kind">Sidecar kind; one of <see cref="ExpectationKinds.All"/>.</param>
/// <param name="Reason">Why the fragment does not simply equal the primary reference.</param>
/// <param name="PinnedHtml">The pinned renderer output, or <see langword="null"/> when the sidecar has none.</param>
[DebuggerDisplay("{Kind}: {Reason}")]
internal sealed record Expectation(string Kind, string Reason, string? PinnedHtml)
{
    /// <summary>Line that separates the sidecar header from the pinned HTML.</summary>
    private const string Separator = "---";

    /// <summary>Header key that introduces the kind.</summary>
    private const string KindKey = "kind:";

    /// <summary>Header key that introduces the reason.</summary>
    private const string ReasonKey = "reason:";

    /// <summary>Parses the text of a sidecar file.</summary>
    /// <param name="text">Sidecar file content.</param>
    /// <returns>The parsed sidecar; a missing kind defaults to <c>deviation</c>.</returns>
    internal static Expectation Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var kind = ExpectationKinds.Deviation;
        var reason = string.Empty;
        string? pinned = null;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] == Separator)
            {
                pinned = string.Join('\n', lines[(i + 1)..]);
                break;
            }

            if (lines[i].StartsWith(KindKey, StringComparison.Ordinal))
            {
                kind = lines[i][KindKey.Length..].Trim();
            }
            else if (lines[i].StartsWith(ReasonKey, StringComparison.Ordinal))
            {
                reason = lines[i][ReasonKey.Length..].Trim();
            }
        }

        return new(kind, reason, pinned);
    }

    /// <summary>Renders a sidecar file with the given header and pinned HTML.</summary>
    /// <param name="kind">Sidecar kind.</param>
    /// <param name="reason">Reason text.</param>
    /// <param name="html">HTML written after the separator.</param>
    /// <returns>The sidecar file content.</returns>
    internal static string Format(string kind, string reason, string html) =>
        $"{KindKey} {kind}\n{ReasonKey} {reason}\n{Separator}\n{html}";
}
