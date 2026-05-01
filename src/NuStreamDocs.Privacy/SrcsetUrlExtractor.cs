// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy;

/// <summary>Pulls the bare URL token off a single HTML <c>srcset</c> entry.</summary>
internal static class SrcsetUrlExtractor
{
    /// <summary>Extracts the URL portion of <paramref name="entry"/>, stripping surrounding whitespace and any trailing density / width descriptor.</summary>
    /// <param name="entry">One <c>url descriptor</c> entry.</param>
    /// <returns>The trimmed URL.</returns>
    public static string Extract(string entry)
    {
        var span = entry.AsSpan().Trim();
        var spaceIndex = span.IndexOf(' ');
        return (spaceIndex < 0 ? span : span[..spaceIndex]).ToString();
    }
}
