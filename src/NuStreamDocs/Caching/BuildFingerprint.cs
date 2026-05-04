// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Security.Cryptography;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Caching;

/// <summary>
/// Computes the manifest fingerprint for the current build pipeline.
/// </summary>
/// <remarks>
/// The incremental cache must invalidate when the generator or plugin binaries change, not
/// just when the markdown bytes change. Using assembly MVIDs makes local rebuilds produce a
/// fresh fingerprint even before a package version changes.
/// </remarks>
internal static class BuildFingerprint
{
    /// <summary>Returns the cache fingerprint for the current build configuration.</summary>
    /// <param name="plugins">Registered plugins, in execution order.</param>
    /// <param name="options">Pipeline options that affect emitted output.</param>
    /// <returns>Raw SHA-256 digest bytes for this build shape.</returns>
    public static byte[] Create(IDocPlugin[] plugins, in BuildPipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        var buffer = new ArrayBufferWriter<byte>(256 + (plugins.Length * 96));
        Write(buffer, "core="u8);
        AppendTypeFingerprint(buffer, typeof(BuildPipeline));
        Write(buffer, "|dir="u8);
        Write(buffer, options.UseDirectoryUrls ? "1"u8 : "0"u8);
        Write(buffer, "|drafts="u8);
        Write(buffer, options.IncludeDrafts ? "1"u8 : "0"u8);

        for (var i = 0; i < plugins.Length; i++)
        {
            Write(buffer, "|plugin="u8);
            Write(buffer, plugins[i].Name);
            Write(buffer, "|"u8);
            AppendTypeFingerprint(buffer, plugins[i].GetType());
        }

        return SHA256.HashData(buffer.WrittenSpan);
    }

    /// <summary>Appends the fingerprint components for <paramref name="type"/>.</summary>
    /// <param name="buffer">Fingerprint sink.</param>
    /// <param name="type">Type whose assembly identity is appended.</param>
    private static void AppendTypeFingerprint(ArrayBufferWriter<byte> buffer, Type type)
    {
        var assembly = type.Assembly;
        var assemblyName = assembly.GetName().Name;
        if (!string.IsNullOrEmpty(assemblyName))
        {
            AsciiByteHelpers.EncodeStringInto(assemblyName, buffer);
        }

        Write(buffer, "|"u8);
        AsciiByteHelpers.EncodeStringInto(assembly.ManifestModule.ModuleVersionId.ToString("D"), buffer);
        Write(buffer, "|"u8);
        var fullName = type.FullName;
        if (string.IsNullOrEmpty(fullName))
        {
            return;
        }

        AsciiByteHelpers.EncodeStringInto(fullName, buffer);
    }

    /// <summary>Writes <paramref name="bytes"/> into <paramref name="buffer"/>.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="bytes">Bytes to append.</param>
    private static void Write(ArrayBufferWriter<byte> buffer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        bytes.CopyTo(buffer.GetSpan(bytes.Length));
        buffer.Advance(bytes.Length);
    }
}
