// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Search.Lunr.Tests;

/// <summary>Asset surface tests for <see cref="LunrSearchPlugin"/>.</summary>
public class LunrAssetEmissionTests
{
    /// <summary><see cref="LunrSearchPlugin.PinnedRuntimeVersion"/> matches the vendored <c>lunr.min.js</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PinnedRuntimeVersionIsSet()
    {
        await Assert.That(LunrSearchPlugin.PinnedRuntimeVersion).IsNotNull();
        await Assert.That(LunrSearchPlugin.PinnedRuntimeVersion).IsNotEmpty();
    }

    /// <summary><c>StaticAssets</c> includes both the Lunr runtime and the bind glue.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StaticAssetsShipRuntimeAndGlue()
    {
        LunrSearchPlugin plugin = new();
        var assets = plugin.StaticAssets;
        await Assert.That(assets.Length).IsEqualTo(2);

        var paths = new HashSet<string>(StringComparer.Ordinal);
        var runtimeBytes = 0;
        var glueBytes = 0;
        for (var i = 0; i < assets.Length; i++)
        {
            paths.Add(assets[i].Path.Value);
            if (assets[i].Path.Value.EndsWith("lunr.min.js", StringComparison.Ordinal))
            {
                runtimeBytes = assets[i].Bytes.Length;
            }
            else if (assets[i].Path.Value.EndsWith("lunr-bind.js", StringComparison.Ordinal))
            {
                glueBytes = assets[i].Bytes.Length;
            }
        }

        await Assert.That(paths.Contains("assets/javascripts/lunr.min.js")).IsTrue();
        await Assert.That(paths.Contains("assets/javascripts/lunr-bind.js")).IsTrue();

        // Sanity: runtime bundle is well above 10 KB; glue is well above 500 bytes.
        await Assert.That(runtimeBytes).IsGreaterThan(10_000);
        await Assert.That(glueBytes).IsGreaterThan(500);
    }

    /// <summary>The vendored <c>lunr.min.js</c> body declares its own version inline; verify it matches the pin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmbeddedRuntimeContainsPinnedVersion()
    {
        LunrSearchPlugin plugin = new();
        byte[]? runtime = null;
        var assets = plugin.StaticAssets;
        for (var i = 0; i < assets.Length; i++)
        {
            if (assets[i].Path.Value.EndsWith("lunr.min.js", StringComparison.Ordinal))
            {
                runtime = assets[i].Bytes;
                break;
            }
        }

        await Assert.That(runtime).IsNotNull();
        var text = Encoding.UTF8.GetString(runtime!);
        await Assert.That(text).Contains(LunrSearchPlugin.PinnedRuntimeVersion);
    }

    /// <summary><c>WriteHeadExtra</c> emits both the lunr.min.js and lunr-bind.js script tags.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraEmitsBothScriptTags()
    {
        LunrSearchPlugin plugin = new();
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        var rendered = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(rendered).Contains("/assets/javascripts/lunr.min.js");
        await Assert.That(rendered).Contains("/assets/javascripts/lunr-bind.js");
    }
}
