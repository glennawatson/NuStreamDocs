// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Metadata;

/// <summary>
/// Lookup of merged inherited frontmatter per relative page path. Keys are
/// forward-slash-normalized paths (e.g. <c>guide/intro.md</c>); values are
/// the merged YAML body bytes without surrounding <c>---</c> delimiters.
/// </summary>
/// <param name="byPath">Path to merged-body lookup; case-insensitive on the path key.</param>
internal sealed class MetadataRegistry(Dictionary<string, byte[]> byPath)
{
    /// <summary>Gets the empty registry.</summary>
    public static MetadataRegistry Empty { get; } = new(EmptyCollections.DictionaryFor<string, byte[]>());

    /// <summary>Looks up the merged-body bytes for <paramref name="relativePath"/>.</summary>
    /// <param name="relativePath">Forward-slash-normalized page path.</param>
    /// <returns>Merged YAML body bytes without surrounding <c>---</c>; empty when no inheritance applies.</returns>
    public ReadOnlySpan<byte> ExtraFor(in FilePath relativePath) =>
        byPath.TryGetValue(relativePath.Value, out var bytes) ? bytes : [];
}
