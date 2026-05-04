// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Reads theme assets out of an assembly's embedded-resource table.
/// </summary>
public static class EmbeddedAssetLoader
{
    /// <summary>Gets the embedded-resource subfolder segment between the root namespace and the file path.</summary>
    private static ReadOnlySpan<byte> TemplatesSegment => "Templates."u8;

    /// <summary>Reads <paramref name="relativePath"/> as a UTF-8 byte array.</summary>
    /// <param name="owningAssembly">Assembly that embeds the resource.</param>
    /// <param name="resourcePrefix">Root namespace prefix MSBuild prepends to the resource.</param>
    /// <param name="relativePath">Forward-slashed path under the <c>Templates/</c> root.</param>
    /// <returns>The asset bytes.</returns>
    public static byte[] ReadBytes(Assembly owningAssembly, ApiCompatString resourcePrefix, FilePath relativePath)
    {
        ArgumentNullException.ThrowIfNull(owningAssembly);
        ArgumentException.ThrowIfNullOrEmpty(resourcePrefix.Value);
        ArgumentException.ThrowIfNullOrEmpty(relativePath.Value);

        var nameBytes = ToResourceName(resourcePrefix, relativePath);
        using var stream = owningAssembly.GetManifestResourceStream(Encoding.UTF8.GetString(nameBytes))
            ?? throw new FileNotFoundException($"Embedded asset '{relativePath.Value}' not found.", relativePath.Value);

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

    /// <summary>Translates a folder-path asset key to the MSBuild manifest resource name as UTF-8 bytes.</summary>
    /// <param name="resourcePrefix">Root namespace prefix MSBuild prepends to the resource.</param>
    /// <param name="relativePath">Forward-slashed path under <c>Templates/</c>.</param>
    /// <returns>The fully-qualified manifest resource name as UTF-8 bytes.</returns>
    public static byte[] ToResourceName(ApiCompatString resourcePrefix, FilePath relativePath)
    {
        var prefix = resourcePrefix.Value ?? string.Empty;
        var rel = relativePath.Value ?? string.Empty;
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        ArgumentException.ThrowIfNullOrEmpty(rel);

        var prefixBytes = Encoding.UTF8.GetBytes(prefix);
        var relBytes = TranslateRelativePath(rel);
        return Utf8Concat.Concat(prefixBytes, TemplatesSegment, relBytes);
    }

    /// <summary>Translates the path's slashes to dots and substitutes <c>@</c>/<c>-</c> with <c>_</c> on every directory segment.</summary>
    /// <param name="rel">Forward-slashed source path.</param>
    /// <returns>UTF-8 bytes with the substitutions applied; ASCII-only paths fit one-to-one in bytes.</returns>
    private static byte[] TranslateRelativePath(string rel)
    {
        var lastSlash = rel.LastIndexOf('/');
        var dst = new byte[rel.Length];
        for (var i = 0; i < rel.Length; i++)
        {
            var c = rel[i];
            dst[i] = c switch
            {
                '/' => (byte)'.',
                '@' or '-' when i < lastSlash => (byte)'_',
                _ => (byte)c,
            };
        }

        return dst;
    }
}
