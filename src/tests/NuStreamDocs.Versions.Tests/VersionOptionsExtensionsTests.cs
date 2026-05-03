// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Versions.Tests;

/// <summary>Behavior tests for <c>VersionOptionsExtensions</c>'s alias-list helpers.</summary>
public class VersionOptionsExtensionsTests
{
    /// <summary><c>WithAliases(string[])</c> replaces the existing list, encoding to UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithAliasesStringReplaces()
    {
        var seeded = VersionOptions.Latest("1.0", "1.0 (latest)");
        var updated = seeded.WithAliases("stable", "current");
        await Assert.That(updated.Aliases.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(updated.Aliases[0])).IsEqualTo("stable");
        await Assert.That(Encoding.UTF8.GetString(updated.Aliases[1])).IsEqualTo("current");
    }

    /// <summary><c>WithAliases(byte[][])</c> stores the supplied UTF-8 bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithAliasesBytesStoresVerbatim()
    {
        byte[][] aliases = [[.. "stable"u8], [.. "current"u8]];
        var updated = new VersionOptions("1.0", "1.0").WithAliases(aliases);
        await Assert.That(updated.Aliases).IsSameReferenceAs(aliases);
    }

    /// <summary><c>AddAliases(string[])</c> appends to the existing list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddAliasesStringAppends()
    {
        var seeded = VersionOptions.Latest("1.0", "1.0");
        var updated = seeded.AddAliases("v1", "stable");
        await Assert.That(updated.Aliases.Length).IsEqualTo(3);
        await Assert.That(Encoding.UTF8.GetString(updated.Aliases[0])).IsEqualTo("latest");
        await Assert.That(Encoding.UTF8.GetString(updated.Aliases[1])).IsEqualTo("v1");
        await Assert.That(Encoding.UTF8.GetString(updated.Aliases[2])).IsEqualTo("stable");
    }

    /// <summary><c>AddAliases(byte[][])</c> appends UTF-8 bytes to the existing list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddAliasesBytesAppends()
    {
        var seeded = VersionOptions.Latest("1.0", "1.0");
        byte[][] extra = [[.. "v1"u8]];
        var updated = seeded.AddAliases(extra);
        await Assert.That(updated.Aliases.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(updated.Aliases[1])).IsEqualTo("v1");
    }

    /// <summary><c>AddAliases</c> with an empty input returns the source unchanged (no copy).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddAliasesEmptyIsNoOp()
    {
        var seeded = VersionOptions.Latest("1.0", "1.0");
        var stringNoOp = seeded.AddAliases(System.Array.Empty<string>());
        var bytesNoOp = seeded.AddAliases(System.Array.Empty<byte[]>());
        await Assert.That(stringNoOp.Aliases).IsSameReferenceAs(seeded.Aliases);
        await Assert.That(bytesNoOp.Aliases).IsSameReferenceAs(seeded.Aliases);
    }

    /// <summary><c>ClearAliases</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearAliasesEmpties()
    {
        var seeded = VersionOptions.Latest("1.0", "1.0").AddAliases("v1");
        var cleared = seeded.ClearAliases();
        await Assert.That(cleared.Aliases.Length).IsEqualTo(0);
    }

    /// <summary>Other fields are preserved across alias edits.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OtherFieldsPreservedAcrossEdits()
    {
        var updated = new VersionOptions("1.0", "Stable")
            .WithAliases("a")
            .AddAliases("b")
            .ClearAliases();
        await Assert.That(updated.Version).IsEqualTo("1.0");
        await Assert.That(updated.Title).IsEqualTo("Stable");
    }

    /// <summary>The single-entry <see cref="ReadOnlySpan{T}"/> overload accepts a <c>"..."u8</c> literal directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadAcceptsU8LiteralDirectly()
    {
        var updated = VersionOptions.Latest("1.0", "1.0").AddAliases("v1"u8);
        await Assert.That(updated.Aliases.Length).IsEqualTo(2);
        await Assert.That(updated.Aliases[1].AsSpan().SequenceEqual("v1"u8)).IsTrue();
    }

    /// <summary>Null arguments throw on every overload.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullArgumentsThrow()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => VersionOptions.Latest("1", "1").WithAliases((byte[][])null!));
        var ex2 = Assert.Throws<ArgumentNullException>(static () => VersionOptions.Latest("1", "1").AddAliases((string[])null!));
        var ex3 = Assert.Throws<ArgumentNullException>(static () => VersionOptions.Latest("1", "1").AddAliases((byte[][])null!));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
        await Assert.That(ex3).IsNotNull();
    }
}
