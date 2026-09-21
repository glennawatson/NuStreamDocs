// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Reflection;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>One <c>[Arguments]</c> row of the generated pinned-output tests.</summary>
/// <param name="Method">Name of the test method that carries the row.</param>
/// <param name="DisplayName">Display name of the row.</param>
/// <param name="Markdown">Markdown source of the row.</param>
/// <param name="ExpectedHtml">Pinned HTML of the row.</param>
[DebuggerDisplay("{DisplayName}")]
internal sealed record PinnedRow(string Method, string DisplayName, string Markdown, string ExpectedHtml)
{
    /// <summary>Reads every row from the generated test class.</summary>
    /// <returns>The rows, grouped by method in declaration order.</returns>
    internal static List<PinnedRow> LoadAll()
    {
        var rows = new List<PinnedRow>();
        var methods = typeof(SidecarPinnedOutputTests).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        for (var i = 0; i < methods.Length; i++)
        {
            foreach (var arguments in methods[i].GetCustomAttributes<ArgumentsAttribute>())
            {
                rows.Add(new(methods[i].Name, arguments.DisplayName ?? string.Empty, (string)arguments.Values[0]!, (string)arguments.Values[1]!));
            }
        }

        return rows;
    }
}
