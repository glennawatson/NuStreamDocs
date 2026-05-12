// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Redirects.Tests;

/// <summary>Coverage for <c>RedirectsOptionsExtensions</c>.</summary>
public class RedirectsOptionsExtensionsTests
{
    /// <summary>The defaults are all-on, with <c>redirect_from</c> as the frontmatter key.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsAreAllOn()
    {
        var o = RedirectsOptions.Default;
        await Assert.That(o.EmitRedirectsFile).IsTrue();
        await Assert.That(o.EmitHeadersFile).IsTrue();
        await Assert.That(o.EmitMetaRefreshPages).IsTrue();
        await Assert.That(o.ReadFrontmatterRedirects).IsTrue();
        await Assert.That(o.DefaultCacheHeaders).IsTrue();
        await Assert.That(o.Redirects.Length).IsEqualTo(0);
        await Assert.That(o.Headers.Length).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(o.FrontmatterKey)).IsEqualTo("redirect_from");
    }

    /// <summary><c>Add</c> accumulates redirects; the two-arg form defaults to permanent.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddAccumulates()
    {
        var o = RedirectsOptions.Default.Add("/old/"u8, "/new/"u8).Add("/gone/"u8, "https://elsewhere.test/"u8, permanent: false);
        await Assert.That(o.Redirects.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(o.Redirects[0].From)).IsEqualTo("/old/");
        await Assert.That(Encoding.UTF8.GetString(o.Redirects[0].To)).IsEqualTo("/new/");
        await Assert.That(o.Redirects[0].Permanent).IsTrue();
        await Assert.That(o.Redirects[1].Permanent).IsFalse();
    }

    /// <summary><c>AddHeaders</c> stores a rule block; <c>WithSecurityHeaders</c> adds the safe security headers on <c>/*</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeaderRulesAndSecurityHeaders()
    {
        var o = RedirectsOptions.Default
            .AddHeaders("/api/*"u8, [.. "X-Robots-Tag: noindex"u8])
            .WithSecurityHeaders();
        await Assert.That(o.Headers.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(o.Headers[0].PathPattern)).IsEqualTo("/api/*");
        await Assert.That(Encoding.UTF8.GetString(o.Headers[0].HeaderLines[0])).IsEqualTo("X-Robots-Tag: noindex");
        await Assert.That(Encoding.UTF8.GetString(o.Headers[1].PathPattern)).IsEqualTo("/*");
        await Assert.That(Encoding.UTF8.GetString(o.Headers[1].HeaderLines[0])).Contains("nosniff");
    }

    /// <summary>The <c>Without…</c> toggles flip their flags.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithoutTogglesAndFrontmatterKey()
    {
        var o = RedirectsOptions.Default
            .WithoutDefaultCacheHeaders()
            .WithoutMetaRefreshPages()
            .WithoutRedirectsFile()
            .WithoutHeadersFile()
            .WithoutFrontmatterRedirects()
            .WithFrontmatterKey("aliases"u8);
        await Assert.That(o.DefaultCacheHeaders).IsFalse();
        await Assert.That(o.EmitMetaRefreshPages).IsFalse();
        await Assert.That(o.EmitRedirectsFile).IsFalse();
        await Assert.That(o.EmitHeadersFile).IsFalse();
        await Assert.That(o.ReadFrontmatterRedirects).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(o.FrontmatterKey)).IsEqualTo("aliases");
    }
}
