// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Optimize.Tests;

/// <summary>Behavior tests for <c>OptimizeOptionsExtensions</c>'s extension-list helpers.</summary>
public class OptimizeOptionsExtensionsTests
{
    /// <summary><c>WithExtensions(string[])</c> replaces the default list, encoding to UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExtensionsStringReplaces()
    {
        var updated = OptimizeOptions.Default.WithExtensions(".html", ".css");
        await Assert.That(updated.Extensions.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(updated.Extensions[0])).IsEqualTo(".html");
        await Assert.That(Encoding.UTF8.GetString(updated.Extensions[1])).IsEqualTo(".css");
    }

    /// <summary><c>WithExtensions(byte[][])</c> stores the supplied UTF-8 bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExtensionsBytesStoresVerbatim()
    {
        byte[][] entries = [[.. ".html"u8], [.. ".css"u8]];
        var updated = OptimizeOptions.Default.WithExtensions(entries);
        await Assert.That(updated.Extensions).IsSameReferenceAs(entries);
    }

    /// <summary><c>AddExtensions(string[])</c> appends to the default list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtensionsStringAppends()
    {
        var defaultCount = OptimizeOptions.Default.Extensions.Length;
        var updated = OptimizeOptions.Default.AddExtensions(".webmanifest", ".map");
        await Assert.That(updated.Extensions.Length).IsEqualTo(defaultCount + 2);
        await Assert.That(Encoding.UTF8.GetString(updated.Extensions[^2])).IsEqualTo(".webmanifest");
        await Assert.That(Encoding.UTF8.GetString(updated.Extensions[^1])).IsEqualTo(".map");
    }

    /// <summary><c>AddExtensions(byte[][])</c> appends UTF-8 bytes to the default list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtensionsBytesAppends()
    {
        var defaultCount = OptimizeOptions.Default.Extensions.Length;
        byte[][] extra = [[.. ".bak"u8]];
        var updated = OptimizeOptions.Default.AddExtensions(extra);
        await Assert.That(updated.Extensions.Length).IsEqualTo(defaultCount + 1);
        await Assert.That(Encoding.UTF8.GetString(updated.Extensions[^1])).IsEqualTo(".bak");
    }

    /// <summary><c>AddExtensions</c> with an empty input returns the source unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtensionsEmptyIsNoOp()
    {
        var seeded = OptimizeOptions.Default;
        var stringNoOp = seeded.AddExtensions(Array.Empty<string>());
        var bytesNoOp = seeded.AddExtensions(Array.Empty<byte[]>());
        await Assert.That(stringNoOp.Extensions).IsSameReferenceAs(seeded.Extensions);
        await Assert.That(bytesNoOp.Extensions).IsSameReferenceAs(seeded.Extensions);
    }

    /// <summary><c>ClearExtensions</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearExtensionsEmpties()
    {
        var cleared = OptimizeOptions.Default.ClearExtensions();
        await Assert.That(cleared.Extensions.Length).IsEqualTo(0);
    }

    /// <summary>Other fields are preserved across extension edits.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OtherFieldsPreservedAcrossEdits()
    {
        var custom = OptimizeOptions.Default with { MinimumBytes = 4096, Parallelism = 2 };
        var updated = custom.AddExtensions(".html").ClearExtensions();
        await Assert.That(updated.MinimumBytes).IsEqualTo(4096);
        await Assert.That(updated.Parallelism).IsEqualTo(2);
    }

    /// <summary>The byte-shaped <c>DefaultExtensions</c> contains the expected text-asset entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultExtensionsContainsExpectedEntries()
    {
        var decoded = new HashSet<string>();
        for (var i = 0; i < OptimizeOptions.DefaultExtensions.Length; i++)
        {
            decoded.Add(Encoding.UTF8.GetString(OptimizeOptions.DefaultExtensions[i]));
        }

        await Assert.That(decoded.Contains(".html")).IsTrue();
        await Assert.That(decoded.Contains(".css")).IsTrue();
        await Assert.That(decoded.Contains(".js")).IsTrue();
    }

    /// <summary>The single-entry <see cref="ReadOnlySpan{T}"/> overload accepts a <c>"..."u8</c> literal directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadAcceptsU8LiteralDirectly()
    {
        var defaultCount = OptimizeOptions.Default.Extensions.Length;
        var updated = OptimizeOptions.Default.AddExtensions(".webmanifest"u8);
        await Assert.That(updated.Extensions.Length).IsEqualTo(defaultCount + 1);
        await Assert.That(updated.Extensions[^1].AsSpan().SequenceEqual(".webmanifest"u8)).IsTrue();
    }

    /// <summary>Null arguments throw on every overload.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullArgumentsThrow()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => OptimizeOptions.Default.WithExtensions((byte[][])null!));
        var ex2 = Assert.Throws<ArgumentNullException>(static () => OptimizeOptions.Default.AddExtensions((string[])null!));
        var ex3 = Assert.Throws<ArgumentNullException>(static () => OptimizeOptions.Default.AddExtensions((byte[][])null!));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
        await Assert.That(ex3).IsNotNull();
    }
}
