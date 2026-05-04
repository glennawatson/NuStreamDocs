// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Targeted coverage for the small but heavily-shared helpers under <c>NuStreamDocs.Common</c>.</summary>
public class CommonCoverageTests
{
    /// <summary><see cref="Utf8Snapshot.Decode(byte[][])"/> round-trips every entry from UTF-8 to a string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8SnapshotDecodeRoundTrips()
    {
        var bytes = new[] { "alpha"u8.ToArray(), "β-rays"u8.ToArray(), [] };
        var decoded = Utf8Snapshot.Decode(bytes);
        await Assert.That(decoded.Length).IsEqualTo(3);
        await Assert.That(decoded[0]).IsEqualTo("alpha");
        await Assert.That(decoded[1]).IsEqualTo("β-rays");
        await Assert.That(decoded[2]).IsEqualTo(string.Empty);
    }

    /// <summary><see cref="Utf8Snapshot.Decode(byte[][])"/> rejects null arguments.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8SnapshotDecodeRejectsNull() =>
        await Assert.That(() => Utf8Snapshot.Decode(null!)).Throws<ArgumentNullException>();

    /// <summary>Two-arg <see cref="Utf8Concat.Concat(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> returns an empty array when both inputs are empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8ConcatTwoArgEmptyReturnsEmpty() =>
        await Assert.That(Utf8Concat.Concat([], []).Length).IsEqualTo(0);

    /// <summary>Two-arg <see cref="Utf8Concat.Concat(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> joins both spans.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8ConcatTwoArgJoins() =>
        await Assert.That(Encoding.UTF8.GetString(Utf8Concat.Concat("foo"u8, "bar"u8))).IsEqualTo("foobar");

    /// <summary>Three-arg <see cref="Utf8Concat.Concat(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> joins all three spans, including with an empty middle.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8ConcatThreeArgJoinsWithEmptyMiddle()
    {
        ReadOnlySpan<byte> middle = [];
        await Assert.That(Encoding.UTF8.GetString(Utf8Concat.Concat("a"u8, middle, "c"u8))).IsEqualTo("ac");
    }

    /// <summary>Four-arg <see cref="Utf8Concat.Concat(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> joins all four pieces.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8ConcatFourArgJoins() =>
        await Assert.That(Encoding.UTF8.GetString(Utf8Concat.Concat("/"u8, "a"u8, "/"u8, "b"u8))).IsEqualTo("/a/b");

    /// <summary><see cref="Utf8Concat.ConcatMany"/> returns an empty array when every chunk is empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8ConcatManyEmptyReturnsEmpty()
    {
        byte[][] none = [];
        await Assert.That(Utf8Concat.ConcatMany(none).Length).IsEqualTo(0);
    }

    /// <summary><see cref="Utf8Concat.ConcatMany"/> joins every chunk in order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8ConcatManyJoinsInOrder()
    {
        var parts = new[] { "x"u8.ToArray(), "yy"u8.ToArray(), "zzz"u8.ToArray() };
        await Assert.That(Encoding.UTF8.GetString(Utf8Concat.ConcatMany(parts))).IsEqualTo("xyyzzz");
    }

    /// <summary><see cref="Utf8Concat.ConcatMany"/> rejects null parts.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Utf8ConcatManyRejectsNullPart()
    {
        var parts = new byte[][] { "ok"u8.ToArray(), null! };
        await Assert.That(() => Utf8Concat.ConcatMany(parts)).Throws<ArgumentNullException>();
    }

    /// <summary><see cref="ByteArrayCollectionExtensions.ToStringSet"/> decodes UTF-8 entries into the supplied comparer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayCollectionToStringSetUsesComparer()
    {
        var source = new[] { "Foo"u8.ToArray(), "FOO"u8.ToArray(), "bar"u8.ToArray() };
        var ordinal = source.ToStringSet(StringComparer.Ordinal);
        var ordinalIgnoreCase = source.ToStringSet(StringComparer.OrdinalIgnoreCase);
        await Assert.That(ordinal.Count).IsEqualTo(3);
        await Assert.That(ordinalIgnoreCase.Count).IsEqualTo(2);
    }

    /// <summary><see cref="ByteArrayCollectionExtensions.ToStringSet"/> rejects null inputs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayCollectionToStringSetRejectsNulls()
    {
        await Assert.That(() => ((byte[][])null!).ToStringSet(StringComparer.Ordinal)).Throws<ArgumentNullException>();
        byte[][] emptyForSet = [];
        await Assert.That(() => emptyForSet.ToStringSet(null!)).Throws<ArgumentNullException>();
    }

    /// <summary><see cref="ByteArrayCollectionExtensions.ToStringArray"/> short-circuits on an empty source.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayCollectionToStringArrayEmptyShortCircuits()
    {
        byte[][] empty = [];
        await Assert.That(empty.ToStringArray().Length).IsEqualTo(0);
    }

    /// <summary><see cref="ByteArrayCollectionExtensions.ToStringArray"/> decodes every entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayCollectionToStringArrayDecodesEntries()
    {
        var arr = new[] { "one"u8.ToArray(), "two"u8.ToArray() }.ToStringArray();
        await Assert.That(arr.Length).IsEqualTo(2);
        await Assert.That(arr[0]).IsEqualTo("one");
        await Assert.That(arr[1]).IsEqualTo("two");
    }

    /// <summary><see cref="ByteArrayCollectionExtensions.ToStringArray"/> rejects null inputs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayCollectionToStringArrayRejectsNull() =>
        await Assert.That(() => ((byte[][])null!).ToStringArray()).Throws<ArgumentNullException>();

    /// <summary><see cref="ByteArrayComparer"/> equality covers null pairs and content equality.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayComparerEqualityHandlesNullsAndContent()
    {
        var c = ByteArrayComparer.Instance;
        await Assert.That(c.Equals(null, null)).IsTrue();
        await Assert.That(c.Equals("a"u8.ToArray(), null)).IsFalse();
        await Assert.That(c.Equals(null, "a"u8.ToArray())).IsFalse();
        await Assert.That(c.Equals("abc"u8.ToArray(), "abc"u8.ToArray())).IsTrue();
        await Assert.That(c.Equals("abc"u8.ToArray(), "abd"u8.ToArray())).IsFalse();
    }

    /// <summary>Equal arrays produce equal hash codes; distinct arrays usually don't.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayComparerHashCodeMatchesContent()
    {
        var c = ByteArrayComparer.Instance;
        await Assert.That(c.GetHashCode("hello"u8.ToArray())).IsEqualTo(c.GetHashCode("hello"u8.ToArray()));
        await Assert.That(() => c.GetHashCode((byte[])null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Ordinal compare ranks lexicographically and treats null as less than any value.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayComparerCompareRanksLexicographically()
    {
        var c = ByteArrayComparer.Instance;
        await Assert.That(c.Compare(null, null)).IsEqualTo(0);
        await Assert.That(c.Compare(null, "x"u8.ToArray())).IsLessThan(0);
        await Assert.That(c.Compare("x"u8.ToArray(), null)).IsGreaterThan(0);
        await Assert.That(c.Compare("a"u8.ToArray(), "b"u8.ToArray())).IsLessThan(0);
        await Assert.That(c.Compare("b"u8.ToArray(), "a"u8.ToArray())).IsGreaterThan(0);
        await Assert.That(c.Compare("a"u8.ToArray(), "a"u8.ToArray())).IsEqualTo(0);
    }

    /// <summary>Non-generic <see cref="IComparer.Compare(object, object)"/> handles nulls and rejects non-byte-array operands.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayComparerNonGenericCompareValidates()
    {
        IComparer c = ByteArrayComparer.Instance;
        await Assert.That(c.Compare(null, null)).IsEqualTo(0);
        await Assert.That(c.Compare(null, "x"u8.ToArray())).IsLessThan(0);
        await Assert.That(c.Compare("x"u8.ToArray(), null)).IsGreaterThan(0);
        await Assert.That(c.Compare("a"u8.ToArray(), "b"u8.ToArray())).IsLessThan(0);
        await Assert.That(() => c.Compare("not bytes", "also not")).Throws<ArgumentException>();
    }

    /// <summary>Non-generic <see cref="IEqualityComparer.Equals(object, object)"/> mirrors the strongly-typed implementation.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayComparerNonGenericEqualsMirrorsStrongTyped()
    {
        IEqualityComparer c = ByteArrayComparer.Instance;
        await Assert.That(c.Equals(null, null)).IsTrue();
        await Assert.That(c.Equals("abc"u8.ToArray(), "abc"u8.ToArray())).IsTrue();
        await Assert.That(c.Equals("abc"u8.ToArray(), "abd"u8.ToArray())).IsFalse();
        await Assert.That(c.Equals("abc"u8.ToArray(), null)).IsFalse();
        await Assert.That(c.Equals("abc"u8.ToArray(), "not bytes")).IsFalse();
    }

    /// <summary>Non-generic <see cref="IEqualityComparer.GetHashCode(object)"/> dispatches to the byte-array hash for arrays.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayComparerNonGenericGetHashCodeDispatches()
    {
        IEqualityComparer c = ByteArrayComparer.Instance;
        await Assert.That(c.GetHashCode("abc"u8.ToArray())).IsEqualTo(c.GetHashCode("abc"u8.ToArray()));
        await Assert.That(() => c.GetHashCode(null!)).Throws<ArgumentNullException>();

        // Non-byte-array objects fall back to the object's own hash.
        var obj = new object();
        await Assert.That(c.GetHashCode(obj)).IsEqualTo(obj.GetHashCode());
    }

    /// <summary>Span-keyed alternate equality and Create round-trip span content into a byte[] copy.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteArrayComparerAlternateLookupRoundTrips()
    {
#pragma warning disable CA1859 // Want the alternate-comparer interface dispatch path here.
        IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]> alt = ByteArrayComparer.Instance;
#pragma warning restore CA1859
        var stored = "hello"u8.ToArray();
        await Assert.That(alt.Equals("hello"u8, stored)).IsTrue();
        await Assert.That(alt.Equals("nope"u8, stored)).IsFalse();
        await Assert.That(alt.GetHashCode("hello"u8)).IsEqualTo(ByteArrayComparer.Instance.GetHashCode(stored));
        var copy = alt.Create("world"u8);
        await Assert.That(copy.Length).IsEqualTo(5);
        await Assert.That(copy.AsSpan().SequenceEqual("world"u8)).IsTrue();
    }

    /// <summary><see cref="UrlPath.IsAbsolute"/> recognises http(s) schemes and protocol-relative URLs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlPathIsAbsoluteRecognisesSchemes()
    {
        await Assert.That(((UrlPath)"http://x.test/y").IsAbsolute).IsTrue();
        await Assert.That(((UrlPath)"HTTPS://X.TEST/").IsAbsolute).IsTrue();
        await Assert.That(((UrlPath)"//cdn.example/").IsAbsolute).IsTrue();
        await Assert.That(((UrlPath)"/local").IsAbsolute).IsFalse();
        await Assert.That(((UrlPath)string.Empty).IsAbsolute).IsFalse();
        await Assert.That(default(UrlPath).IsEmpty).IsTrue();
    }

    /// <summary>Implicit conversions, <see cref="UrlPath.ToString"/>, and the friendly named aliases all unwrap to the same string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlPathConversionsAgree()
    {
        UrlPath wrapped = "/foo/bar";
        string asString = wrapped;
        await Assert.That(asString).IsEqualTo("/foo/bar");
        await Assert.That(((ReadOnlySpan<char>)wrapped).SequenceEqual("/foo/bar")).IsTrue();
        await Assert.That(wrapped.ToString()).IsEqualTo("/foo/bar");
        await Assert.That(UrlPath.FromString("/foo/bar").Value).IsEqualTo("/foo/bar");
        await Assert.That(UrlPath.ToStringValue(wrapped)).IsEqualTo("/foo/bar");
        await Assert.That(UrlPath.ToReadOnlySpan(wrapped).SequenceEqual("/foo/bar")).IsTrue();

        // Default URL ToString lands on string.Empty (Value is null).
        await Assert.That(default(UrlPath).ToString()).IsEqualTo(string.Empty);
        UrlPath fromNull = (string?)null;
        await Assert.That(fromNull.IsEmpty).IsTrue();
    }

    /// <summary><see cref="UrlPath.StartsWith(ReadOnlySpan{char})"/>/<see cref="UrlPath.EndsWith(ReadOnlySpan{char})"/> in both ordinal + comparison overloads.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlPathStartsAndEndsWithCoverBothOverloads()
    {
        UrlPath url = "https://Example.test/Foo.svg";
        await Assert.That(url.StartsWith("https://".AsSpan())).IsTrue();
        await Assert.That(url.StartsWith("HTTPS://".AsSpan(), StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(url.EndsWith(".svg".AsSpan())).IsTrue();
        await Assert.That(url.EndsWith(".SVG".AsSpan(), StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(url.AsSpan().SequenceEqual("https://Example.test/Foo.svg")).IsTrue();
    }

    /// <summary><see cref="DirectoryPath"/> path-manipulation members (Name, Parent, Combine, /, File, AsSpan).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryPathMembersComposeCorrectly()
    {
        var docs = (DirectoryPath)Path.Combine("/", "var", "docs");
        await Assert.That(docs.Name).IsEqualTo("docs");
        await Assert.That(docs.Parent.Name).IsEqualTo("var");
        await Assert.That((docs / "guide").Name).IsEqualTo("guide");
        await Assert.That((docs / (DirectoryPath)"guide").Name).IsEqualTo("guide");

        // Empty subpath stays the same after the directory-divide overload.
        await Assert.That((docs / default(DirectoryPath)).Name).IsEqualTo("docs");

        var combined = docs.Combine("intro.md");
        await Assert.That(combined.Name).IsEqualTo("intro.md");

        var file = docs.File("intro.md");
        await Assert.That(file.FileName).IsEqualTo("intro.md");

        // Building from empty allows fresh construction.
        await Assert.That(default(DirectoryPath).Combine("seed").Name).IsEqualTo("seed");
        await Assert.That(default(DirectoryPath).File("seed.md").FileName).IsEqualTo("seed.md");

        // Empty / default DirectoryPath behaviour.
        await Assert.That(default(DirectoryPath).IsEmpty).IsTrue();
        await Assert.That(default(DirectoryPath).Name).IsEqualTo(string.Empty);
        await Assert.That(default(DirectoryPath).Parent.IsEmpty).IsTrue();
        await Assert.That(default(DirectoryPath).ToString()).IsEqualTo(string.Empty);

        // Friendly named aliases for the implicit/divide operators.
        await Assert.That(DirectoryPath.FromString("/x").Value).IsEqualTo("/x");
        await Assert.That(DirectoryPath.ToStringValue(docs)).IsEqualTo(docs.Value);
        await Assert.That(DirectoryPath.ToReadOnlySpan(docs).SequenceEqual(docs.Value)).IsTrue();
        await Assert.That(DirectoryPath.Divide(docs, "guide").Name).IsEqualTo("guide");
        await Assert.That(DirectoryPath.Divide(docs, (DirectoryPath)"guide").Name).IsEqualTo("guide");

        // AsSpan on default returns empty.
        await Assert.That(default(DirectoryPath).AsSpan().Length).IsEqualTo(0);
    }

    /// <summary><see cref="DirectoryPath.Create"/> / <see cref="DirectoryPath.Delete"/> / <see cref="DirectoryPath.Exists"/> talk to the filesystem.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryPathFilesystemOpsRoundTrip()
    {
        var root = (DirectoryPath)Path.Combine(Path.GetTempPath(), "smkd-dirpath-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        try
        {
            await Assert.That(root.Exists()).IsFalse();
            root.Create();
            await Assert.That(root.Exists()).IsTrue();

            // EnumerateFiles / EnumerateDirectories yield wrappers over the real names.
            var nestedDir = root / "nested";
            nestedDir.Create();
            var filePath = root.File("a.txt");
            await File.WriteAllTextAsync(filePath, "x");

            var files = root.EnumerateFiles().ToArray();
            await Assert.That(files.Length).IsEqualTo(1);
            await Assert.That(files[0].FileName).IsEqualTo("a.txt");

            var pattern = root.EnumerateFiles("*.txt").ToArray();
            await Assert.That(pattern.Length).IsEqualTo(1);

            var deep = root.EnumerateFiles("*", SearchOption.AllDirectories).ToArray();
            await Assert.That(deep.Length).IsEqualTo(1);

            var dirs = root.EnumerateDirectories().ToArray();
            await Assert.That(dirs.Length).IsEqualTo(1);
            await Assert.That(dirs[0].Name).IsEqualTo("nested");
            await Assert.That(root.EnumerateDirectories("ne*").ToArray().Length).IsEqualTo(1);
            await Assert.That(root.EnumerateDirectories("ne*", SearchOption.TopDirectoryOnly).ToArray().Length).IsEqualTo(1);

            // Empty wrappers yield empty enumerations without touching disk.
            await Assert.That(default(DirectoryPath).EnumerateFiles().ToArray().Length).IsEqualTo(0);
            await Assert.That(default(DirectoryPath).EnumerateFiles("*").ToArray().Length).IsEqualTo(0);
            await Assert.That(default(DirectoryPath).EnumerateFiles("*", SearchOption.AllDirectories).ToArray().Length).IsEqualTo(0);
            await Assert.That(default(DirectoryPath).EnumerateDirectories().ToArray().Length).IsEqualTo(0);
            await Assert.That(default(DirectoryPath).EnumerateDirectories("*").ToArray().Length).IsEqualTo(0);
            await Assert.That(default(DirectoryPath).EnumerateDirectories("*", SearchOption.AllDirectories).ToArray().Length).IsEqualTo(0);

            // Delete the file so we can also exercise Delete + DeleteRecursive.
            File.Delete(filePath);
            nestedDir.Delete();
            await Assert.That(nestedDir.Exists()).IsFalse();

            // DeleteRecursive on a populated tree.
            var leaf = root / "deep";
            leaf.Create();
            await File.WriteAllTextAsync(leaf.File("x.txt"), "x");
            root.DeleteRecursive(recursive: true);
            await Assert.That(root.Exists()).IsFalse();
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
        }
    }

    /// <summary><see cref="DirectoryPath.Combine"/> rejects an empty segment.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryPathCombineRejectsEmptySegment() =>
        await Assert.That(() => ((DirectoryPath)"/var").Combine(string.Empty)).Throws<ArgumentException>();

    /// <summary><see cref="DirectoryPath.File"/> rejects an empty file name.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryPathFileRejectsEmptyFileName() =>
        await Assert.That(() => ((DirectoryPath)"/var").File(string.Empty)).Throws<ArgumentException>();

    /// <summary><see cref="DirectoryPath.Normalize"/> short-circuits on empty input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryPathNormalizeHandlesEmpty()
    {
        await Assert.That(DirectoryPath.Normalize(string.Empty)).IsEqualTo(string.Empty);
        await Assert.That(DirectoryPath.Normalize("/abc")).IsEqualTo("/abc");
    }
}
