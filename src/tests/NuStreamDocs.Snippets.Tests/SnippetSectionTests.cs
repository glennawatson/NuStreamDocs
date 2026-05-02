// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Snippets.Tests;

/// <summary>Section-marker include support — <c>--8&lt;-- "file#name"</c> + <c>&lt;!-- @section name --&gt;</c>.</summary>
public class SnippetSectionTests
{
    /// <summary>Section-include splices only the marked block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SectionIncludeSplicesOnlySection()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("doc.md", "Header\n<!-- @section example -->\nA\nB\n<!-- @endsection -->\nFooter\n");
        var result = fixture.Rewrite("Before.\n--8<-- \"doc.md#example\"\nAfter.");
        await Assert.That(result).IsEqualTo("Before.\nA\nB\nAfter.");
    }

    /// <summary>An unknown section name produces an error block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownSectionProducesErrorBlock()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("doc.md", "no markers here\n");
        var result = fixture.Rewrite("--8<-- \"doc.md#missing\"\n");
        await Assert.That(result).Contains("snippet section not found");
    }

    /// <summary>Section markers that surround the entire file behave like a whole-file include.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FullFileSectionMatches()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("doc.md", "<!-- @section all -->\nbody\n<!-- @endsection -->\n");
        var result = fixture.Rewrite("--8<-- \"doc.md#all\"\n");
        await Assert.That(result).IsEqualTo("body\n");
    }

    /// <summary>Section names are matched verbatim — case differences miss.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SectionNameIsCaseSensitive()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("doc.md", "<!-- @section Example -->\nbody\n<!-- @endsection -->\n");
        var result = fixture.Rewrite("--8<-- \"doc.md#example\"\n");
        await Assert.That(result).Contains("snippet section not found");
    }

    /// <summary>Two sections in the same file resolve independently.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoSectionsResolveIndependently()
    {
        using var fixture = new SnippetFixture();
        fixture.Write(
            "doc.md",
            "<!-- @section first -->\nA\n<!-- @endsection -->\n<!-- @section second -->\nB\n<!-- @endsection -->\n");
        var result = fixture.Rewrite("--8<-- \"doc.md#second\"\n");
        await Assert.That(result).IsEqualTo("B\n");
    }

    /// <summary>Section content can itself contain other includes; they expand inside the slice.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SectionBodyExpandsNestedIncludes()
    {
        using var fixture = new SnippetFixture();
        fixture.Write("inner.md", "INNER\n");
        fixture.Write(
            "outer.md",
            "before\n<!-- @section x -->\n--8<-- \"inner.md\"\n<!-- @endsection -->\nafter\n");
        var result = fixture.Rewrite("--8<-- \"outer.md#x\"\n");
        await Assert.That(result).IsEqualTo("INNER\n");
    }

    /// <summary>Test fixture mirroring the one in <c>SnippetsRewriterTests</c>.</summary>
    private sealed class SnippetFixture : IDisposable
    {
        /// <summary>Throwaway directory for snippet files.</summary>
        private readonly string _root = Path.Combine(Path.GetTempPath(), "smkd-snippet-section-" + Guid.NewGuid().ToString("N"));

        /// <summary>Initializes a new instance of the <see cref="SnippetFixture"/> class.</summary>
        public SnippetFixture() => Directory.CreateDirectory(_root);

        /// <summary>Writes <paramref name="content"/> to <paramref name="relativePath"/> in the fixture root.</summary>
        /// <param name="relativePath">Relative path inside the snippet root.</param>
        /// <param name="content">UTF-8 file content.</param>
        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        /// <summary>Runs the rewriter over <paramref name="source"/> against the fixture root.</summary>
        /// <param name="source">Source markdown.</param>
        /// <returns>Decoded UTF-8 result.</returns>
        public string Rewrite(string source)
        {
            var sink = new ArrayBufferWriter<byte>(256);
            var cache = new Dictionary<byte[], byte[]>(ByteArrayComparer.Instance);
            SnippetsRewriter.Rewrite(Encoding.UTF8.GetBytes(source), _root, cache, sink);
            return Encoding.UTF8.GetString(sink.WrittenSpan);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!Directory.Exists(_root))
            {
                return;
            }

            Directory.Delete(_root, recursive: true);
        }
    }
}
