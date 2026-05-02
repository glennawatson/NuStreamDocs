// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Xrefs.Tests;

/// <summary>Direct tests for XrefMapReader covering its tolerance branches.</summary>
public class XrefMapReaderTests
{
    /// <summary>Non-object root returns an empty payload.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonObjectRoot()
    {
        var payload = XrefMapReader.Read("[]"u8);
        await Assert.That(payload.Entries.Length).IsEqualTo(0);
    }

    /// <summary>Document with only baseUrl is captured.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BaseUrlOnly()
    {
        var json = """{"baseUrl": "https://docs.test/"}"""u8;
        var payload = XrefMapReader.Read(json);
        await Assert.That(payload.BaseUrl.AsSpan().SequenceEqual("https://docs.test/"u8)).IsTrue();
    }

    /// <summary>Unknown top-level keys are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownTopLevelKeysSkipped()
    {
        var json = """{"version":1,"baseUrl":"/x/","extra":{"deep":[1,2]},"references":[{"uid":"A","href":"/a"}]}"""u8;
        var payload = XrefMapReader.Read(json);
        await Assert.That(payload.BaseUrl.AsSpan().SequenceEqual("/x/"u8)).IsTrue();
        await Assert.That(payload.Entries.Length).IsEqualTo(1);
    }

    /// <summary>Entries missing uid or href are dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncompleteEntriesDropped()
    {
        var json = """
            {"references":[
                {"uid":"A","href":"/a"},
                {"uid":"B"},
                {"href":"/c"},
                {"name":"unrelated"}
            ]}
            """u8;
        var payload = XrefMapReader.Read(json);
        await Assert.That(payload.Entries.Length).IsEqualTo(1);
        await Assert.That(payload.Entries[0].Uid.AsSpan().SequenceEqual("A"u8)).IsTrue();
    }

    /// <summary>Non-object items inside the references array are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonObjectArrayItemSkipped()
    {
        var json = """{"references":[1,"x",null,{"uid":"A","href":"/a"}]}"""u8;
        var payload = XrefMapReader.Read(json);
        await Assert.That(payload.Entries.Length).IsEqualTo(1);
    }

    /// <summary>References field that isn't an array is ignored.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReferencesNotArray()
    {
        var json = """{"references":"not an array"}"""u8;
        var payload = XrefMapReader.Read(json);
        await Assert.That(payload.Entries.Length).IsEqualTo(0);
    }

    /// <summary>Unknown reference fields (name, commentId, fullName) are tolerated.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownReferenceFieldsTolerated()
    {
        var json = """
                   {"references":[
                       {"uid":"X","name":"X.Display","fullName":"Some.Namespace.X","commentId":"T:Some.Namespace.X","href":"/x.html"}
                   ]}
                   """u8.ToArray();
        var payload = XrefMapReader.Read(json);
        await Assert.That(payload.Entries.Length).IsEqualTo(1);
        await Assert.That(payload.Entries[0].Uid.AsSpan().SequenceEqual("X"u8)).IsTrue();
        await Assert.That(payload.Entries[0].Href.AsSpan().SequenceEqual("/x.html"u8)).IsTrue();
    }
}
