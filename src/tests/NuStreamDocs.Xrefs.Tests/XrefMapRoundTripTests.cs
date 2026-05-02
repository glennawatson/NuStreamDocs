// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Xrefs.Tests;

/// <summary>Round-trip tests for <c>XrefMapWriter</c> + <c>XrefMapReader</c>.</summary>
public class XrefMapRoundTripTests
{
    /// <summary>Writing then reading the same payload reproduces the entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RoundTripsEveryEntry()
    {
        using var temp = TempDir.Create();
        var map = Path.Combine(temp.Root, "xrefmap.json");
        XrefMapWriter.Write(map, "https://example.com/", [("Foo.Bar", "api/Foo.Bar.html"), ("Baz", "api/Baz.html")]);

        var bytes = await File.ReadAllBytesAsync(map);
        var payload = XrefMapReader.Read(bytes);

        await Assert.That(payload.BaseUrl.AsSpan().SequenceEqual("https://example.com/"u8)).IsTrue();
        await Assert.That(payload.Entries.Length).IsEqualTo(2);
    }

    /// <summary>Empty <c>baseUrl</c> is omitted from the emitted document.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyBaseUrlIsOmitted()
    {
        using var temp = TempDir.Create();
        var map = Path.Combine(temp.Root, "xrefmap.json");
        XrefMapWriter.Write(map, string.Empty, [("Foo", "f.html")]);

        var text = await File.ReadAllTextAsync(map, Encoding.UTF8);
        await Assert.That(text.Contains("baseUrl", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>Entries are emitted in ordinal-sorted UID order so diffs are stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EntriesAreSortedByUid()
    {
        using var temp = TempDir.Create();
        var map = Path.Combine(temp.Root, "xrefmap.json");
        XrefMapWriter.Write(map, string.Empty, [("Zebra", "z.html"), ("Apple", "a.html"), ("Mango", "m.html")]);

        var bytes = await File.ReadAllBytesAsync(map);
        var payload = XrefMapReader.Read(bytes);

        await Assert.That(payload.Entries[0].Uid.AsSpan().SequenceEqual("Apple"u8)).IsTrue();
        await Assert.That(payload.Entries[1].Uid.AsSpan().SequenceEqual("Mango"u8)).IsTrue();
        await Assert.That(payload.Entries[2].Uid.AsSpan().SequenceEqual("Zebra"u8)).IsTrue();
    }

    /// <summary>Reader tolerates an empty document.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReaderHandlesEmptyDocument()
    {
        var payload = XrefMapReader.Read("{}"u8);
        await Assert.That(payload.Entries.Length).IsEqualTo(0);
        await Assert.That(payload.BaseUrl.Length).IsEqualTo(0);
    }

    /// <summary>Reader skips entries missing either <c>uid</c> or <c>href</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReaderSkipsMalformedEntries()
    {
        var payload = XrefMapReader.Read("{\"references\":[{\"uid\":\"x\"},{\"href\":\"y.html\"},{\"uid\":\"z\",\"href\":\"z.html\"}]}"u8);
        await Assert.That(payload.Entries.Length).IsEqualTo(1);
        await Assert.That(payload.Entries[0].Uid.AsSpan().SequenceEqual("z"u8)).IsTrue();
    }

    /// <summary>Reader tolerates and skips unknown DocFX fields (<c>name</c>, <c>fullName</c>, <c>commentId</c>).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReaderTolerantOfDocFxExtraFields()
    {
        var payload = XrefMapReader.Read("{\"references\":[{\"uid\":\"x\",\"name\":\"X\",\"fullName\":\"Foo.X\",\"href\":\"x.html\"}]}"u8);
        await Assert.That(payload.Entries.Length).IsEqualTo(1);
        await Assert.That(payload.Entries[0].Uid.AsSpan().SequenceEqual("x"u8)).IsTrue();
        await Assert.That(payload.Entries[0].Href.AsSpan().SequenceEqual("x.html"u8)).IsTrue();
    }
}
