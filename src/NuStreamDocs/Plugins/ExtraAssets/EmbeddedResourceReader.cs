// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins.ExtraAssets;

/// <summary>Reads embedded resources from a caller-supplied <see cref="System.Reflection.Assembly"/>.</summary>
internal static class EmbeddedResourceReader
{
    /// <summary>Reads the resource named by <paramref name="source"/> into a fresh UTF-8 byte array.</summary>
    /// <param name="source">Embedded-resource source.</param>
    /// <returns>Resource bytes.</returns>
    /// <exception cref="InvalidOperationException">When the resource cannot be located on the assembly.</exception>
    public static byte[] Read(ExtraAssetSource source)
    {
        using var stream = source.Assembly!.GetManifestResourceStream(source.ResourceName!)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{source.ResourceName}' not found in '{source.Assembly!.GetName().Name}'.");
        var buffer = new byte[stream.Length];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = stream.Read(buffer, read, buffer.Length - read);
            if (n is 0)
            {
                break;
            }

            read += n;
        }

        return buffer;
    }
}
