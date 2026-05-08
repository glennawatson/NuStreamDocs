// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Layouts.Tests;

/// <summary>Behavioural tests for <see cref="TemplateCache"/>.</summary>
public class TemplateCacheTests
{
    /// <summary>An empty cache returns false from <see cref="TemplateCache.TryGet"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Empty_TryGet_ReturnsFalse()
    {
        TemplateCache cache = new();
        var hit = cache.TryGet("home.html"u8, out var entry);
        await Assert.That(hit).IsFalse();
        await Assert.That(entry).IsNull();
        await Assert.That(cache.Count).IsEqualTo(0);
    }

    /// <summary>After <see cref="TemplateCache.Add"/>, <see cref="TemplateCache.TryGet"/> returns the same entry instance — no re-parse.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Add_Then_TryGet_ReturnsSameInstance()
    {
        TemplateCache cache = new();
        var bytes = "<p>x</p>"u8.ToArray();
        var unit = TemplateUnit.From(bytes);
        TemplateEntry entry = new(unit, new ApiCompatString("/tmp/home.html"));
        cache.Add("home.html"u8.ToArray(), entry);

        var hit = cache.TryGet("home.html"u8, out var observed);
        await Assert.That(hit).IsTrue();
        await Assert.That(observed).IsSameReferenceAs(entry);
        await Assert.That(observed.Unit.Bytes).IsSameReferenceAs(bytes);
        await Assert.That(cache.Count).IsEqualTo(1);
    }

    /// <summary><see cref="TemplateCache.Clear"/> empties the cache.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Clear_RemovesAllEntries()
    {
        TemplateCache cache = new();
        TemplateEntry entry = new(TemplateUnit.From("<p>x</p>"u8.ToArray()), new ApiCompatString("/tmp/home.html"));
        cache.Add("home.html"u8.ToArray(), entry);
        await Assert.That(cache.Count).IsEqualTo(1);

        cache.Clear();

        await Assert.That(cache.Count).IsEqualTo(0);
        var hit = cache.TryGet("home.html"u8, out _);
        await Assert.That(hit).IsFalse();
    }

    /// <summary>Concurrent <see cref="TemplateCache.Add"/> calls under the same key let exactly one writer's entry win; every observer afterwards sees the same entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConcurrentAdd_OneWinnerSurvives()
    {
        TemplateCache cache = new();
        var bytes = "<p>x</p>"u8.ToArray();
        TemplateEntry entryA = new(TemplateUnit.From(bytes), new ApiCompatString("/tmp/A.html"));
        TemplateEntry entryB = new(TemplateUnit.From(bytes), new ApiCompatString("/tmp/B.html"));

        Task taskA = Task.Run(() => cache.Add("home.html"u8.ToArray(), entryA));
        Task taskB = Task.Run(() => cache.Add("home.html"u8.ToArray(), entryB));
        await Task.WhenAll(taskA, taskB);

        await Assert.That(cache.Count).IsEqualTo(1);
        var hit = cache.TryGet("home.html"u8, out var observed);
        await Assert.That(hit).IsTrue();
        var observedRef = observed.ResolvedPath.Value;
        await Assert.That(observedRef is "/tmp/A.html" or "/tmp/B.html").IsTrue();
    }

    /// <summary>Distinct keys coexist; each lookup returns its own entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DistinctKeys_DoNotCollide()
    {
        TemplateCache cache = new();
        TemplateEntry home = new(TemplateUnit.From("<p>home</p>"u8.ToArray()), new ApiCompatString("/tmp/home.html"));
        TemplateEntry sidebar = new(TemplateUnit.From("<p>side</p>"u8.ToArray()), new ApiCompatString("/tmp/sidebar.html"));
        cache.Add("home.html"u8.ToArray(), home);
        cache.Add("sidebar.html"u8.ToArray(), sidebar);

        await Assert.That(cache.Count).IsEqualTo(2);
        await Assert.That(cache.TryGet("home.html"u8, out var h)).IsTrue();
        await Assert.That(h).IsSameReferenceAs(home);
        await Assert.That(cache.TryGet("sidebar.html"u8, out var s)).IsTrue();
        await Assert.That(s).IsSameReferenceAs(sidebar);
    }
}
