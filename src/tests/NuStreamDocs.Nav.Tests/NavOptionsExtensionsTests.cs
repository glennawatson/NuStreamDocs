// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav.Tests;

/// <summary>Behavior tests for <c>NavOptionsExtensions</c>'s glob-list helpers.</summary>
public class NavOptionsExtensionsTests
{
    /// <summary><c>WithIncludes(string[])</c> replaces the existing list verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithIncludesReplacesList()
    {
        var updated = NavOptions.Default.WithIncludes("**/*.md", "docs/**");
        await Assert.That(updated.Includes.Length).IsEqualTo(2);
        await Assert.That(updated.Includes[0]).IsEqualTo("**/*.md");
        await Assert.That(updated.Includes[1]).IsEqualTo("docs/**");
    }

    /// <summary><c>WithIncludes</c> with an empty array clears the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithIncludesEmptyClears()
    {
        var seeded = NavOptions.Default.WithIncludes("a", "b");
        var updated = seeded.WithIncludes();
        await Assert.That(updated.Includes.Length).IsEqualTo(0);
    }

    /// <summary><c>AddIncludes(string[])</c> appends to the existing list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddIncludesAppends()
    {
        var seeded = NavOptions.Default.WithIncludes("first.md");
        var updated = seeded.AddIncludes("second.md", "third.md");
        await Assert.That(updated.Includes.Length).IsEqualTo(3);
        await Assert.That(updated.Includes[0]).IsEqualTo("first.md");
        await Assert.That(updated.Includes[1]).IsEqualTo("second.md");
        await Assert.That(updated.Includes[2]).IsEqualTo("third.md");
    }

    /// <summary><c>AddIncludes</c> with an empty array returns the source unchanged (no copy).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddIncludesEmptyIsNoOp()
    {
        var seeded = NavOptions.Default.WithIncludes("only.md");
        var updated = seeded.AddIncludes();
        await Assert.That(updated.Includes).IsSameReferenceAs(seeded.Includes);
    }

    /// <summary><c>AddIncludes</c> on an empty source returns the appended list directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddIncludesFromEmptyReturnsTailDirectly()
    {
        var updated = NavOptions.Default.AddIncludes("a", "b");
        await Assert.That(updated.Includes.Length).IsEqualTo(2);
        await Assert.That(updated.Includes[0]).IsEqualTo("a");
    }

    /// <summary><c>ClearIncludes</c> empties the list — drops both defaults and any caller-added entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearIncludesEmpties()
    {
        var updated = NavOptions.Default
            .AddIncludes("kept.md")
            .ClearIncludes();
        await Assert.That(updated.Includes.Length).IsEqualTo(0);
    }

    /// <summary><c>WithExcludes</c> mirrors the include semantics.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExcludesReplacesList()
    {
        var updated = NavOptions.Default.WithExcludes("drafts/**");
        await Assert.That(updated.Excludes.Length).IsEqualTo(1);
        await Assert.That(updated.Excludes[0]).IsEqualTo("drafts/**");
    }

    /// <summary><c>AddExcludes</c> + <c>ClearExcludes</c> mirror the include semantics.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddAndClearExcludes()
    {
        var added = NavOptions.Default.AddExcludes("a", "b");
        await Assert.That(added.Excludes.Length).IsEqualTo(2);

        var cleared = added.ClearExcludes();
        await Assert.That(cleared.Excludes.Length).IsEqualTo(0);
    }

    /// <summary>The other <see cref="NavOptions"/> fields are unaffected by glob-list edits.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OtherFieldsArePreservedAcrossEdits()
    {
        var custom = NavOptions.Default with { Prune = true, HideEmptySections = false };
        var updated = custom.AddIncludes("x").AddExcludes("y").ClearIncludes();
        await Assert.That(updated.Prune).IsTrue();
        await Assert.That(updated.HideEmptySections).IsFalse();
        await Assert.That(updated.Excludes.Length).IsEqualTo(1);
    }

    /// <summary>Null array arguments are rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullArgumentsThrow()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => NavOptions.Default.WithIncludes(null!));
        var ex2 = Assert.Throws<ArgumentNullException>(static () => NavOptions.Default.AddIncludes(null!));
        var ex3 = Assert.Throws<ArgumentNullException>(static () => NavOptions.Default.WithExcludes(null!));
        var ex4 = Assert.Throws<ArgumentNullException>(static () => NavOptions.Default.AddExcludes(null!));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
        await Assert.That(ex3).IsNotNull();
        await Assert.That(ex4).IsNotNull();
    }
}
