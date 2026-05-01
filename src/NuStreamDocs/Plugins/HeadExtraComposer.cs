// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

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
/// </remarks>
public static class HeadExtraComposer
{
    /// <summary>Composes the head-extras byte array from every provider in <paramref name="plugins"/>.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <returns>UTF-8 bytes; empty when no provider was registered.</returns>
    public static byte[] Compose(IDocPlugin[] plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        ArrayBufferWriter<byte>? writer = null;
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is not IHeadExtraProvider provider)
            {
                continue;
            }

            writer ??= new();
            provider.WriteHeadExtra(writer);
        }

        return writer is null ? [] : [.. writer.WrittenSpan];
    }
}
