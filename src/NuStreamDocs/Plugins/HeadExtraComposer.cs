// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Walks a plugin list and concatenates every
/// <see cref="IHeadExtraProvider"/>'s contribution into a single
/// UTF-8 byte array that theme plugins splice into <c>&lt;head&gt;</c>.
/// </summary>
/// <remarks>
/// Theme plugins call this once during <see cref="IDocPlugin.OnConfigureAsync"/>
/// and stash the result for the lifetime of the build, since the
/// providers' output is build-invariant in the common case.
/// Duplicate <c>&lt;link rel="preconnect"&gt;</c> lines emitted by
/// independent plugins (icon fonts most often share the Google Fonts
/// origins) are folded to a single emission keyed on the line bytes.
/// </remarks>
public static class HeadExtraComposer
{
    /// <summary>UTF-8 prefix matched against each line to identify preconnect hints subject to dedup.</summary>
    private static readonly byte[] PreconnectPrefix = "<link rel=\"preconnect\""u8.ToArray();

    /// <summary>Composes the head-extras byte array from every provider in <paramref name="plugins"/>.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <returns>UTF-8 bytes; empty when no provider was registered.</returns>
    public static byte[] Compose(IDocPlugin[] plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        ArrayBufferWriter<byte>? output = null;
        ArrayBufferWriter<byte>? scratch = null;
        HashSet<byte[]>? seenPreconnects = null;

        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is not IHeadExtraProvider provider)
            {
                continue;
            }

            scratch ??= new();
            scratch.ResetWrittenCount();
            provider.WriteHeadExtra(scratch);

            if (scratch.WrittenCount is 0)
            {
                continue;
            }

            output ??= new();
            AppendDeduped(output, scratch.WrittenSpan, ref seenPreconnects);
        }

        return output is null ? [] : [.. output.WrittenSpan];
    }

    /// <summary>Appends <paramref name="source"/> to <paramref name="output"/>, skipping preconnect lines already present in <paramref name="seenPreconnects"/>.</summary>
    /// <param name="output">Destination writer.</param>
    /// <param name="source">One provider's UTF-8 output.</param>
    /// <param name="seenPreconnects">Lazily-initialized set of preconnect-line byte arrays already emitted.</param>
    private static void AppendDeduped(
        ArrayBufferWriter<byte> output,
        ReadOnlySpan<byte> source,
        ref HashSet<byte[]>? seenPreconnects)
    {
        var cursor = source;
        while (cursor is [_, ..])
        {
            var newline = cursor.IndexOf((byte)'\n');
            var lineLength = newline >= 0 ? newline + 1 : cursor.Length;
            var line = cursor[..lineLength];

            if (line.StartsWith(PreconnectPrefix))
            {
                seenPreconnects ??= new(ByteArrayComparer.Instance);
                if (!seenPreconnects.AsUtf8Lookup().Add(line))
                {
                    cursor = cursor[lineLength..];
                    continue;
                }
            }

            HeadExtraWriter.WriteUtf8(output, line);
            cursor = cursor[lineLength..];
        }
    }
}
