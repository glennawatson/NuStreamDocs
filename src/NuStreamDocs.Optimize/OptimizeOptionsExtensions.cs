// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Optimize;

/// <summary>String / span construction helpers for the byte-shaped <see cref="OptimizeOptions"/> record.</summary>
public static class OptimizeOptionsExtensions
{
    /// <summary>Replaces the extension list with <paramref name="extensions"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="extensions">Lowercase, leading-dot file extensions (e.g. <c>.html</c>).</param>
    /// <returns>The updated options.</returns>
    public static OptimizeOptions WithExtensions(this OptimizeOptions options, params string[] extensions)
    {
        return options with { Extensions = extensions.EncodeUtf8Array() };
    }

    /// <summary>Replaces the extension list with the supplied UTF-8 extension bytes.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="extensions">UTF-8 extension entries (one per extension).</param>
    /// <returns>The updated options.</returns>
    public static OptimizeOptions WithExtensions(this OptimizeOptions options, params byte[][] extensions)
    {
        return options with { Extensions = extensions };
    }

    /// <summary>Appends <paramref name="extensions"/> to the existing extension list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="extensions">Additional extension strings.</param>
    /// <returns>The updated options.</returns>
    public static OptimizeOptions AddExtensions(this OptimizeOptions options, params string[] extensions)
    {
        return extensions.Length is 0
            ? options
            : options with { Extensions = ArrayJoiner.Concat(options.Extensions, extensions.EncodeUtf8Array()) };
    }

    /// <summary>Appends UTF-8 <paramref name="extensions"/> to the existing extension list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="extensions">Additional UTF-8 extension entries.</param>
    /// <returns>The updated options.</returns>
    public static OptimizeOptions AddExtensions(this OptimizeOptions options, params byte[][] extensions)
    {
        return extensions.Length is 0
            ? options
            : options with { Extensions = ArrayJoiner.Concat(options.Extensions, extensions) };
    }

    /// <summary>Appends a single UTF-8 extension (e.g. a <c>"..."u8</c> literal) to the existing list.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="extension">UTF-8 extension bytes (lowercase, leading dot, e.g. <c>".html"u8</c>).</param>
    /// <returns>The updated options.</returns>
    public static OptimizeOptions AddExtensions(this OptimizeOptions options, ReadOnlySpan<byte> extension)
    {
        return options with { Extensions = ArrayJoiner.Concat(options.Extensions, [extension.ToArray()]) };
    }

    /// <summary>Empties the extension list — the plugin becomes a no-op until repopulated.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static OptimizeOptions ClearExtensions(this OptimizeOptions options)
    {
        return options with { Extensions = [] };
    }
}
