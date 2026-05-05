// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Macros.Tests;

/// <summary>Tests for the byte- and string-shaped <c>MacrosOptions</c> construction helpers.</summary>
public class MacrosOptionsExtensionsTests
{
    /// <summary>Byte-shaped <c>WithVariable</c> stores the supplied byte array as the dictionary key.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariableBytesStoresEntry()
    {
        var options = MacrosOptions.Default.WithVariable([.. "name"u8], [.. "world"u8]);
        var lookup = options.Variables.GetAlternateLookup<ReadOnlySpan<byte>>();
        await Assert.That(lookup.TryGetValue("name"u8, out var value)).IsTrue();
        await Assert.That(value.AsSpan().SequenceEqual("world"u8)).IsTrue();
    }

    /// <summary>String-shaped <c>WithVariable</c> encodes both inputs and produces the same byte-keyed entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariableStringEncodesToBytes()
    {
        var options = MacrosOptions.Default.WithVariable("name", "world");
        var lookup = options.Variables.GetAlternateLookup<ReadOnlySpan<byte>>();
        await Assert.That(lookup.TryGetValue("name"u8, out var value)).IsTrue();
        await Assert.That(value.AsSpan().SequenceEqual("world"u8)).IsTrue();
    }

    /// <summary>String + byte overloads produce equivalent option records.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariableStringMatchesByteOverload()
    {
        var fromString = MacrosOptions.Default.WithVariable("greeting", "hello");
        var fromBytes = MacrosOptions.Default.WithVariable([.. "greeting"u8], [.. "hello"u8]);

        await Assert.That(fromString.Variables.Count).IsEqualTo(1);
        await Assert.That(fromBytes.Variables.Count).IsEqualTo(1);

        var stringLookup = fromString.Variables.GetAlternateLookup<ReadOnlySpan<byte>>();
        var bytesLookup = fromBytes.Variables.GetAlternateLookup<ReadOnlySpan<byte>>();
        await Assert.That(stringLookup.TryGetValue("greeting"u8, out var s)).IsTrue();
        await Assert.That(bytesLookup.TryGetValue("greeting"u8, out var b)).IsTrue();
        await Assert.That(s.AsSpan().SequenceEqual(b)).IsTrue();
    }

    /// <summary>Repeated <c>WithVariable</c> calls are last-write-wins and preserve earlier entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariableLastWriteWins()
    {
        var options = MacrosOptions.Default
            .WithVariable("a", "1")
            .WithVariable("b", "2")
            .WithVariable("a", "overridden");

        var lookup = options.Variables.GetAlternateLookup<ReadOnlySpan<byte>>();
        await Assert.That(lookup.TryGetValue("a"u8, out var a)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(a!)).IsEqualTo("overridden");
        await Assert.That(lookup.TryGetValue("b"u8, out var b)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(b!)).IsEqualTo("2");
    }

    /// <summary>Byte-shaped <c>WithVariables</c> seeds the entire map at once.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariablesBytesSeedsMap()
    {
        Dictionary<byte[], byte[]> seed = new(ByteArrayComparer.Instance)
        {
            [[.. "a"u8]] = [.. "1"u8],
            [[.. "b"u8]] = [.. "2"u8]
        };
        var options = MacrosOptions.Default.WithVariables(seed);
        await Assert.That(options.Variables.Count).IsEqualTo(2);
    }

    /// <summary>String-shaped <c>WithVariables</c> encodes the input dictionary entry-by-entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariablesStringEncodesAllEntries()
    {
        Dictionary<ApiCompatString, ApiCompatString> seed = new()
        {
            ["a"] = "1",
            ["b"] = "2"
        };
        var options = MacrosOptions.Default.WithVariables(seed);
        var lookup = options.Variables.GetAlternateLookup<ReadOnlySpan<byte>>();
        await Assert.That(lookup.TryGetValue("a"u8, out var a)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(a!)).IsEqualTo("1");
        await Assert.That(lookup.TryGetValue("b"u8, out var b)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(b!)).IsEqualTo("2");
    }

    /// <summary><c>WithVariable(string)</c> rejects null/empty names with a descriptive exception.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariableStringRejectsNullOrEmptyName()
    {
        await Assert.That(static () => MacrosOptions.Default.WithVariable((string)null!, "v"))
            .Throws<ArgumentException>();
        await Assert.That(static () => MacrosOptions.Default.WithVariable(string.Empty, "v"))
            .Throws<ArgumentException>();
    }

    /// <summary><c>WithVariable(byte[])</c> rejects null/empty names with a descriptive exception.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariableBytesRejectsNullOrEmptyName()
    {
        await Assert.That(static () => MacrosOptions.Default.WithVariable((byte[])null!, [.. "v"u8]))
            .Throws<ArgumentNullException>();
        await Assert.That(static () => MacrosOptions.Default.WithVariable([], [.. "v"u8]))
            .Throws<ArgumentException>();
    }

    /// <summary><c>WithVariables</c> rejects a null map.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithVariablesRejectsNullMap()
    {
        await Assert.That(static () => MacrosOptions.Default.WithVariables((Dictionary<byte[], byte[]>)null!))
            .Throws<ArgumentNullException>();
        await Assert.That(static () => MacrosOptions.Default.WithVariables((Dictionary<ApiCompatString, ApiCompatString>)null!))
            .Throws<ArgumentNullException>();
    }
}
