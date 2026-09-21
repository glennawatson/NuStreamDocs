// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Writes <c>.expect</c> sidecars that pin our current output.</summary>
internal static class SidecarPinner
{
    /// <summary>Writes a sidecar for each fragment with our current output as the pinned HTML.</summary>
    /// <param name="output">Destination for progress lines.</param>
    /// <param name="fragmentsDirectory">Corpus directory.</param>
    /// <param name="fragments">Fragments to pin.</param>
    /// <param name="kind">Sidecar kind.</param>
    /// <param name="reason">Sidecar reason.</param>
    internal static void Pin(TextWriter output, string fragmentsDirectory, Fragment[] fragments, string kind, string reason)
    {
        foreach (var fragment in fragments)
        {
            var html = OursRenderer.Render(fragment.Markdown).Html ?? string.Empty;
            File.WriteAllText(FragmentCorpus.SidecarPath(fragmentsDirectory, fragment.Id), Expectation.Format(kind, reason, html), new UTF8Encoding(false));
            output.WriteLine($"pinned {fragment.Id}");
        }
    }
}
