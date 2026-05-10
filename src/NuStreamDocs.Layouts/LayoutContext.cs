// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Layouts;

/// <summary>Variable bag handed to <see cref="LayoutRenderer"/>; resolves <c>page.X</c> references.</summary>
internal sealed class LayoutContext
{
    /// <summary>Span-keyed alternate lookup so per-marker probes do not allocate.</summary>
    private readonly Dictionary<byte[], byte[]>.AlternateLookup<ReadOnlySpan<byte>> _lookup;

    /// <summary>Initializes a new instance of the <see cref="LayoutContext"/> class.</summary>
    /// <param name="values">UTF-8 name → UTF-8 value map. Keys do not include the <c>page.</c> prefix.</param>
    public LayoutContext(Dictionary<byte[], byte[]> values)
    {
        _lookup = values.AsUtf8Lookup();
    }

    /// <summary>Builds a <see cref="LayoutContext"/> from the page's frontmatter and the rendered HTML body.</summary>
    /// <param name="source">Original UTF-8 markdown bytes (frontmatter + body).</param>
    /// <param name="renderedHtml">UTF-8 HTML produced by the markdown renderer.</param>
    /// <param name="relativeUrl">Site-relative URL of the page (forward-slash, no leading slash).</param>
    /// <returns>A populated context.</returns>
    public static LayoutContext FromPage(ReadOnlySpan<byte> source, ReadOnlySpan<byte> renderedHtml, ReadOnlySpan<byte> relativeUrl)
    {
        Dictionary<byte[], byte[]> values = new(ByteArrayComparer.Instance)
        {
            ["content"u8.ToArray()] = renderedHtml.ToArray(),
            ["title"u8.ToArray()] = FrontmatterReader.GetScalar(source, "title"u8).ToArray(),
            ["url"u8.ToArray()] = relativeUrl.ToArray()
        };
        FrontmatterReader.AppendScalars(source, values);
        return new(values);
    }

    /// <summary>Resolves <paramref name="name"/> to its UTF-8 value bytes.</summary>
    /// <param name="name">UTF-8 bare name (the <c>X</c> in <c>page.X</c>).</param>
    /// <param name="value">Resolved bytes on hit; otherwise empty.</param>
    /// <returns>True when the name is known.</returns>
    public bool TryGetValue(ReadOnlySpan<byte> name, out byte[] value) =>
        _lookup.TryGetValue(name, out value!);
}
