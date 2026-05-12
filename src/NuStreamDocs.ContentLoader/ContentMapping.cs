// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.ContentLoader;

/// <summary>
/// Describes how to turn each object in a structured collection (JSON / YAML array, API response)
/// into a Markdown page: which field decides the output path, which field holds the body, and which
/// fields become frontmatter.
/// </summary>
/// <param name="RouteTemplate">
/// Output path relative to the input root, with <c>{field}</c> placeholders filled from the object's
/// scalar fields (e.g. <c>changelog/{tag_name}.md</c>). An object whose template references a missing
/// or non-scalar field is skipped.
/// </param>
/// <param name="BodyKey">
/// Name of the field whose string value is the Markdown body. Empty for a frontmatter-only page.
/// </param>
/// <param name="CollectionPointer">
/// Dotted path of object-property names locating the array within the source document
/// (e.g. <c>data.search.nodes</c>). Empty when the document's root is itself the array.
/// </param>
/// <param name="FrontmatterKeys">
/// Field names to copy into frontmatter. Empty copies every field except <see cref="BodyKey"/>.
/// </param>
public sealed record ContentMapping(
    byte[] RouteTemplate,
    byte[] BodyKey,
    byte[] CollectionPointer,
    byte[][] FrontmatterKeys)
{
    /// <summary>Creates a mapping with the given route template, no body field, a root-level array, and all fields as frontmatter.</summary>
    /// <param name="routeTemplate">Output path template with <c>{field}</c> placeholders.</param>
    /// <returns>The mapping.</returns>
    public static ContentMapping ForRoute(ReadOnlySpan<byte> routeTemplate) =>
        new([.. routeTemplate], [], [], []);

    /// <summary>Returns a copy that takes the page body from <paramref name="key"/>.</summary>
    /// <param name="key">Field name holding the Markdown body.</param>
    /// <returns>The updated mapping.</returns>
    public ContentMapping WithBodyKey(ReadOnlySpan<byte> key) =>
        this with { BodyKey = [.. key] };

    /// <summary>Returns a copy that locates the collection at <paramref name="pointer"/>.</summary>
    /// <param name="pointer">Dotted path of object-property names to the array.</param>
    /// <returns>The updated mapping.</returns>
    public ContentMapping WithCollectionPointer(ReadOnlySpan<byte> pointer) =>
        this with { CollectionPointer = [.. pointer] };

    /// <summary>Returns a copy that copies only the named fields into frontmatter.</summary>
    /// <param name="keys">Field names to keep.</param>
    /// <returns>The updated mapping.</returns>
    public ContentMapping WithFrontmatterKeys(byte[][] keys) =>
        this with { FrontmatterKeys = keys };

    /// <summary>Throws when the mapping is unusable.</summary>
    /// <exception cref="ArgumentException">When the route template is empty.</exception>
    public void Validate()
    {
        if (RouteTemplate is [_, ..])
        {
            return;
        }

        throw new ArgumentException("A content mapping requires a non-empty route template.", nameof(RouteTemplate));
    }
}
