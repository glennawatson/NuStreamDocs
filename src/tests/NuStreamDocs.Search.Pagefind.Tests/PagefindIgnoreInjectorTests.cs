// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>Behavior tests for <see cref="PagefindIgnoreInjector"/>.</summary>
public class PagefindIgnoreInjectorTests
{
    /// <summary><c>TryInject</c> adds the attribute immediately after <c>&lt;body</c>, preserving existing attributes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryInjectAddsAttributeAfterBody()
    {
        byte[] input = [.. "<!doctype html><html><head></head><body class=\"x\"><h1>Hi</h1></body></html>"u8];
        var changed = PagefindIgnoreInjector.TryInject(input, out var output);
        await Assert.That(changed).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("<body data-pagefind-ignore class=\"x\">");
    }

    /// <summary><c>TryInject</c> handles a bare <c>&lt;body&gt;</c> with no attributes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryInjectHandlesBareBody()
    {
        byte[] input = [.. "<html><body><p>x</p></body></html>"u8];
        var changed = PagefindIgnoreInjector.TryInject(input, out var output);
        await Assert.That(changed).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("<body data-pagefind-ignore>");
    }

    /// <summary><c>TryInject</c> is idempotent — a page already carrying the attribute is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryInjectIsIdempotent()
    {
        byte[] input = [.. "<html><body data-pagefind-ignore class=\"x\"><p>x</p></body></html>"u8];
        var changed = PagefindIgnoreInjector.TryInject(input, out var output);
        await Assert.That(changed).IsFalse();
        await Assert.That(output).IsSameReferenceAs(input);
    }

    /// <summary><c>TryInject</c> is a no-op on input without a body tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryInjectNoBodyIsNoOp()
    {
        byte[] input = [.. "<html><head><title>t</title></head></html>"u8];
        var changed = PagefindIgnoreInjector.TryInject(input, out var output);
        await Assert.That(changed).IsFalse();
        await Assert.That(output).IsSameReferenceAs(input);
    }

    /// <summary><c>TryInject</c> ignores <c>&lt;body</c>-prefixed text that is not the body tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryInjectIgnoresNonBodyTagLookalike()
    {
        // "<bodysuit" must not match; the real <body> further along must.
        byte[] input = [.. "<html><p>&lt;bodysuit&gt;</p><body><h1>x</h1></body></html>"u8];
        var changed = PagefindIgnoreInjector.TryInject(input, out var output);
        await Assert.That(changed).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("<body data-pagefind-ignore>");
    }

    /// <summary><c>MatchesPrefix</c> matches on any prefix and normalizes backslashes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MatchesPrefixMatchesAndNormalizes()
    {
        byte[][] prefixes = [[.. "api/"u8], [.. "changelog/"u8]];
        await Assert.That(PagefindIgnoreInjector.MatchesPrefix("api/ReactiveUI/index.html", prefixes)).IsTrue();
        await Assert.That(PagefindIgnoreInjector.MatchesPrefix("api\\ReactiveUI\\index.html", prefixes)).IsTrue();
        await Assert.That(PagefindIgnoreInjector.MatchesPrefix("changelog/v1.html", prefixes)).IsTrue();
        await Assert.That(PagefindIgnoreInjector.MatchesPrefix("documentation/intro.html", prefixes)).IsFalse();
        await Assert.That(PagefindIgnoreInjector.MatchesPrefix("api/index.html", [])).IsFalse();
    }

    /// <summary><c>InjectAsync</c> rewrites only the files under a matching prefix and returns the count.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InjectAsyncRewritesOnlyMatchingFiles()
    {
        using TempDir dir = new();
        WriteFile(dir.Root, "index.html", "<html><body><h1>Home</h1></body></html>");
        WriteFile(dir.Root, Path.Combine("documentation", "intro.html"), "<html><body><h1>Intro</h1></body></html>");
        WriteFile(
            dir.Root,
            Path.Combine("api", "ReactiveUI", "index.html"),
            "<html><body><h1>ReactiveUI</h1></body></html>");
        WriteFile(
            dir.Root,
            Path.Combine("api", "DynamicData", "index.html"),
            "<html><body class=\"a\"><h1>DynamicData</h1></body></html>");

        var modified = await PagefindIgnoreInjector.InjectAsync(
            new(dir.Root),
            [[.. "api/"u8]],
            NullLogger.Instance,
            CancellationToken.None);

        await Assert.That(modified).IsEqualTo(2);
        await Assert.That(ReadFile(dir.Root, "index.html")).DoesNotContain("data-pagefind-ignore");
        await Assert.That(ReadFile(dir.Root, Path.Combine("documentation", "intro.html")))
            .DoesNotContain("data-pagefind-ignore");
        await Assert.That(ReadFile(dir.Root, Path.Combine("api", "ReactiveUI", "index.html")))
            .Contains("<body data-pagefind-ignore>");
        await Assert.That(ReadFile(dir.Root, Path.Combine("api", "DynamicData", "index.html")))
            .Contains("<body data-pagefind-ignore class=\"a\">");
    }

    /// <summary><c>InjectAsync</c> with no prefixes is a no-op returning zero.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InjectAsyncEmptyPrefixesIsNoOp()
    {
        using TempDir dir = new();
        WriteFile(dir.Root, Path.Combine("api", "x.html"), "<html><body><h1>x</h1></body></html>");
        var modified =
            await PagefindIgnoreInjector.InjectAsync(new(dir.Root), [], NullLogger.Instance, CancellationToken.None);
        await Assert.That(modified).IsEqualTo(0);
        await Assert.That(ReadFile(dir.Root, Path.Combine("api", "x.html"))).DoesNotContain("data-pagefind-ignore");
    }

    /// <summary><c>InjectAsync</c> on a missing site root returns zero rather than throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InjectAsyncMissingRootReturnsZero()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "smkd-pf-missing-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var modified = await PagefindIgnoreInjector.InjectAsync(
            new(missing),
            [[.. "api/"u8]],
            NullLogger.Instance,
            CancellationToken.None);
        await Assert.That(modified).IsEqualTo(0);
    }

    /// <summary><c>InjectAsync</c> is idempotent across repeated runs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InjectAsyncIsIdempotentAcrossRuns()
    {
        using TempDir dir = new();
        WriteFile(dir.Root, Path.Combine("api", "x.html"), "<html><body><h1>x</h1></body></html>");
        byte[][] prefixes = [[.. "api/"u8]];

        var first = await PagefindIgnoreInjector.InjectAsync(
            new(dir.Root),
            prefixes,
            NullLogger.Instance,
            CancellationToken.None);
        var second =
            await PagefindIgnoreInjector.InjectAsync(
                new(dir.Root),
                prefixes,
                NullLogger.Instance,
                CancellationToken.None);

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(0);
        var html = ReadFile(dir.Root, Path.Combine("api", "x.html"));
        await Assert.That(CountOccurrences(html, "data-pagefind-ignore")).IsEqualTo(1);
    }

    /// <summary><c>InjectAsync</c> rejects an empty site root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InjectAsyncEmptyRootThrows()
    {
        static Task Invoke() => PagefindIgnoreInjector.InjectAsync(
            new(string.Empty),
            [[.. "api/"u8]],
            NullLogger.Instance,
            CancellationToken.None);

        await Assert.That(Invoke).Throws<ArgumentException>();
    }

    /// <summary>Writes <paramref name="content"/> to <paramref name="relativePath"/> under <paramref name="root"/>, creating directories.</summary>
    /// <param name="root">Scratch root.</param>
    /// <param name="relativePath">Path relative to <paramref name="root"/>.</param>
    /// <param name="content">File contents.</param>
    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Reads <paramref name="relativePath"/> under <paramref name="root"/>.</summary>
    /// <param name="root">Scratch root.</param>
    /// <param name="relativePath">Path relative to <paramref name="root"/>.</param>
    /// <returns>File contents.</returns>
    private static string ReadFile(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath));

    /// <summary>Counts non-overlapping occurrences of <paramref name="needle"/> in <paramref name="haystack"/>.</summary>
    /// <param name="haystack">Text to scan.</param>
    /// <param name="needle">Substring to count.</param>
    /// <returns>Occurrence count.</returns>
    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "smkd-pf-inject-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch root.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
