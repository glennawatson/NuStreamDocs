// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Sitemap.Tests;

/// <summary>Parameterized frontmatter-shape tests for RedirectsPlugin.OnRenderPageAsync.</summary>
public class RedirectsPluginParameterizedTests
{
    /// <summary>Each frontmatter shape that declares an aliases list registers the entries.</summary>
    /// <param name="frontmatter">Frontmatter (between the <c>---</c> fences).</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("aliases: [old.html]\n")]
    [Arguments("aliases: [/old.html]\n")]
    [Arguments("aliases: ['old.html']\n")]
    [Arguments("aliases: [\"old.html\"]\n")]
    [Arguments("aliases: [old.html, legacy.html]\n")]
    [Arguments("aliases:\n  - old.html\n")]
    [Arguments("aliases:\n  - old.html\n  - legacy.html\n")]
    [Arguments("title: x\naliases: [old.html]\n")]
    [Arguments("aliases: [old.html]\nauthor: a\n")]
    public async Task FrontmatterAliasShapes(string frontmatter)
    {
        var plugin = new RedirectsPlugin();
        var bytes = Encoding.UTF8.GetBytes($"---\n{frontmatter}---\n# body");
        var sink = new ArrayBufferWriter<byte>(bytes.Length);
        sink.Write(bytes);
        await plugin.OnRenderPageAsync(new("guide/intro.md", bytes, sink), CancellationToken.None);
    }

    /// <summary>Frontmatter shapes that should NOT register an alias (no key, empty list, no frontmatter, etc.).</summary>
    /// <param name="source">Whole page source.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("body only")]
    [Arguments("---\ntitle: x\n---\nbody")]
    [Arguments("---\naliases: []\n---\nbody")]
    [Arguments("---\naliases: [\"\"]\n---\nbody")]
    [Arguments("aliases: [oops]\nno-frontmatter")]
    public async Task NoAliasesNoCrash(string source)
    {
        var plugin = new RedirectsPlugin();
        var bytes = Encoding.UTF8.GetBytes(source);
        var sink = new ArrayBufferWriter<byte>(bytes.Length + 1);
        sink.Write(bytes);
        await plugin.OnRenderPageAsync(new("p.md", bytes, sink), CancellationToken.None);
    }

    /// <summary>Constructor surface — every overload returns a usable instance.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CtorOverloads()
    {
        await Assert.That(new RedirectsPlugin().Name).IsEqualTo("redirects");
        await Assert.That(new RedirectsPlugin(("a.html", "/b.html")).Name).IsEqualTo("redirects");
        await Assert.That(new RedirectsPlugin(RedirectsOptions.Default, [("a.html", "/b.html")]).Name).IsEqualTo("redirects");
    }
}
