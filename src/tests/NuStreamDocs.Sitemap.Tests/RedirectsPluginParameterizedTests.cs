// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Sitemap.Tests;

/// <summary>Parameterized frontmatter-shape tests for RedirectsPlugin.DiscoverAsync.</summary>
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
        using ScratchDir input = new();
        using ScratchDir output = new();
        Directory.CreateDirectory(Path.Combine(input.Root, "guide"));
        await File.WriteAllTextAsync(Path.Combine(input.Root, "guide", "intro.md"), $"---\n{frontmatter}---\n# body");

        RedirectsPlugin plugin = new();
        await plugin.DiscoverAsync(new(input.Root, output.Root, []), CancellationToken.None);
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
        using ScratchDir input = new();
        using ScratchDir output = new();
        await File.WriteAllTextAsync(Path.Combine(input.Root, "page.md"), source);

        RedirectsPlugin plugin = new();
        await plugin.DiscoverAsync(new(input.Root, output.Root, []), CancellationToken.None);
    }

    /// <summary>Constructor surface — every overload returns a usable instance.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CtorOverloads()
    {
        await Assert.That(new RedirectsPlugin().Name.SequenceEqual("redirects"u8)).IsTrue();
        await Assert.That(new RedirectsPlugin(("a.html", "/b.html")).Name.SequenceEqual("redirects"u8)).IsTrue();
        await Assert.That(new RedirectsPlugin(RedirectsOptions.Default, [("a.html", "/b.html")]).Name.SequenceEqual("redirects"u8)).IsTrue();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-rdp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch directory.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
