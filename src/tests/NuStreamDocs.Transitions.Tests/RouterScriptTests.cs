// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Transitions.Tests;

/// <summary>Sanity checks on the embedded router script.</summary>
public class RouterScriptTests
{
    /// <summary>The script is non-trivial and contains the behaviors it's supposed to.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScriptContainsExpectedBehaviors()
    {
        var js = Encoding.UTF8.GetString(RouterScript.Bytes);
        await Assert.That(js.Length).IsGreaterThan(2000);
        foreach (var marker in new[]
                 {
                     "startViewTransition",
                     "popstate",
                     "prefers-reduced-motion",
                     "DOMParser",
                     "AbortController",
                     "nstd:page-load",
                     "nstd:before-swap",
                     "nstd:router",
                     "location.assign",
                     "IntersectionObserver",
                     "scrollRestoration",
                 })
        {
            await Assert.That(js).Contains(marker);
        }
    }
}
