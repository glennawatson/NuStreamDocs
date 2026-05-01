// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Walks a plugin list and writes every <see cref="IStaticAssetProvider"/>'s
/// contribution into the output tree.
/// </summary>
/// <remarks>
/// Theme plugins call this once during <see cref="IDocPlugin.OnFinaliseAsync"/>
/// alongside their own static-asset emit, so markdown-extension and
/// icon plugins work uniformly under both Material and Material 3
/// themes.
/// </remarks>
public static class StaticAssetComposer
{
    /// <summary>Writes every provider's assets under <paramref name="outputRoot"/>.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="outputRoot">Absolute output root.</param>
    public static void WriteAll(IDocPlugin[] plugins, string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentException.ThrowIfNullOrEmpty(outputRoot);

        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is not IStaticAssetProvider provider)
            {
                continue;
            }

            var assets = provider.StaticAssets;
            for (var j = 0; j < assets.Length; j++)
            {
                var (relativePath, bytes) = assets[j];
                var target = Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, bytes);
            }
        }
    }
}
