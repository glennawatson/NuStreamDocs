// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Reads theme assets out of an assembly's embedded-resource table.
/// </summary>
public static class EmbeddedAssetLoader
{
    /// <summary>Embedded-resource subfolder segment between the root namespace and the file path.</summary>
    private const string TemplatesSegment = "Templates.";

    /// <summary>Reads <paramref name="relativePath"/> as a UTF-8 byte array.</summary>
    /// <param name="owningAssembly">Assembly that embeds the resource.</param>
    /// <param name="resourcePrefix">Root namespace prefix MSBuild prepends to the resource.</param>
    /// <param name="relativePath">Forward-slashed path under the <c>Templates/</c> root.</param>
    /// <returns>The asset bytes.</returns>
    public static byte[] ReadBytes(Assembly owningAssembly, string resourcePrefix, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(owningAssembly);
        ArgumentException.ThrowIfNullOrEmpty(resourcePrefix);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        var name = ToResourceName(resourcePrefix, relativePath);
        using var stream = owningAssembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded asset '{relativePath}' not found.", relativePath);

        var buffer = new byte[checked((int)stream.Length)];
        var read = 0;
        while (read < buffer.Length)
        {
            var step = stream.Read(buffer, read, buffer.Length - read);
            if (step is 0)
            {
                break;
            }

            read += step;
        }

        return buffer;
    }

    /// <summary>Translates a folder-path asset key to the MSBuild resource name.</summary>
    /// <param name="resourcePrefix">Root namespace prefix MSBuild prepends to the resource.</param>
    /// <param name="relativePath">Forward-slashed path under <c>Templates/</c>.</param>
    /// <returns>The fully-qualified manifest resource name.</returns>
    public static string ToResourceName(string resourcePrefix, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourcePrefix);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        return string.Create(resourcePrefix.Length + TemplatesSegment.Length + relativePath.Length, (resourcePrefix, relativePath), static (dst, state) =>
        {
            state.resourcePrefix.AsSpan().CopyTo(dst);
            TemplatesSegment.AsSpan().CopyTo(dst[state.resourcePrefix.Length..]);
            var write = state.resourcePrefix.Length + TemplatesSegment.Length;
            for (var i = 0; i < state.relativePath.Length; i++)
            {
                var c = state.relativePath[i];
                dst[write + i] = c is '/' ? '.' : c;
            }
        });
    }
}
