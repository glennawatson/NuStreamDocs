// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Metadata.Tests;

/// <summary>Behavior tests for <c>FrontmatterSplicer</c>.</summary>
public class FrontmatterSplicerTests
{
    /// <summary>Empty inherited bytes pass the source through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoExtraIsPassThrough()
    {
        const string Source = "---\ntitle: Hi\n---\nbody";
        await Assert.That(Splice(Source, string.Empty)).IsEqualTo(Source);
    }

    /// <summary>A page with no frontmatter receives a wrapped block from the inherited bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoExistingFrontmatterWrapsExtra()
    {
        var output = Splice("body only", "title: Inherited\n");
        await Assert.That(output).IsEqualTo("---\ntitle: Inherited\n---\nbody only");
    }

    /// <summary>Inherited keys absent from the page's frontmatter are appended.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExistingFrontmatterAppendsFreshKeys()
    {
        const string Source = "---\ntitle: Page\n---\nbody";
        var output = Splice(Source, "author: Alice\n");
        await Assert.That(output).IsEqualTo("---\ntitle: Page\nauthor: Alice\n---\nbody");
    }

    /// <summary>Page-defined keys win over inherited keys.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PageKeysOverrideInherited()
    {
        const string Source = "---\ntitle: Page Wins\n---\nbody";
        var output = Splice(Source, "title: Inherited\nauthor: Alice\n");
        await Assert.That(output).IsEqualTo("---\ntitle: Page Wins\nauthor: Alice\n---\nbody");
    }

    /// <summary>Block-style values (block list / multi-line scalar) survive the splice.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockValuesAppendVerbatim()
    {
        const string Source = "---\ntitle: Page\n---\nbody";
        var output = Splice(Source, "tags:\n  - a\n  - b\n");
        await Assert.That(output).IsEqualTo("---\ntitle: Page\ntags:\n  - a\n  - b\n---\nbody");
    }

    /// <summary>Drives the splicer with UTF-8 encoded inputs.</summary>
    /// <param name="source">Page bytes.</param>
    /// <param name="extra">Inherited bytes.</param>
    /// <returns>Spliced output as a string.</returns>
    private static string Splice(string source, string extra)
    {
        var sink = new ArrayBufferWriter<byte>(Math.Max(1, source.Length + extra.Length));
        FrontmatterSplicer.Splice(Encoding.UTF8.GetBytes(source), Encoding.UTF8.GetBytes(extra), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
