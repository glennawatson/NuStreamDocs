// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Lunr;

/// <summary>Provides the vendored <c>lunr.min.js</c> bundle from embedded resources.</summary>
internal static class LunrAssets
{
    /// <summary>The pinned upstream Lunr version — kept in sync with the bytes embedded under <c>Assets/lunr.min.js</c>.</summary>
    public const string PinnedVersion = "2.3.9";

    /// <summary>Embedded-resource identifier for the vendored Lunr bundle.</summary>
    private const string LunrResourceName = "NuStreamDocs.Search.Lunr.Assets.lunr.min.js";

    /// <summary>Cached bytes of the Lunr bundle; lazily loaded on first read.</summary>
    private static byte[]? _cached;

    /// <summary>Gets the UTF-8 bytes of the vendored Lunr runtime.</summary>
    /// <returns>Bytes of <c>lunr.min.js</c>.</returns>
    /// <exception cref="InvalidOperationException">When the embedded resource is missing — should be impossible at run time.</exception>
    public static byte[] LunrMinJsBytes() => _cached ??= ReadEmbeddedResource(LunrResourceName);

    /// <summary>Reads <paramref name="name"/> from this assembly's manifest resources.</summary>
    /// <param name="name">Embedded-resource identifier.</param>
    /// <returns>Resource bytes.</returns>
    private static byte[] ReadEmbeddedResource(string name)
    {
        var asm = typeof(LunrAssets).Assembly;
        using var stream = asm.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException(BuildResourceNotFoundMessage(asm, name));
        using MemoryStream sink = new();
        stream.CopyTo(sink);
        return sink.ToArray();
    }

    /// <summary>Composes the resource-not-found message via the project's <see cref="StringCompose"/> helper (one explicit allocation per fragment).</summary>
    /// <param name="asm">Assembly being inspected.</param>
    /// <param name="name">Missing resource identifier.</param>
    /// <returns>Composed message.</returns>
    private static string BuildResourceNotFoundMessage(Assembly asm, string name)
    {
        var resourceNames = asm.GetManifestResourceNames();
        var available = string.Join(", ", resourceNames);
        return StringCompose.Concat(
            "Embedded resource '",
            name,
            "' not found in ",
            asm.GetName().Name ?? "<null>",
            StringCompose.Concat(". Available: ", available));
    }
}
