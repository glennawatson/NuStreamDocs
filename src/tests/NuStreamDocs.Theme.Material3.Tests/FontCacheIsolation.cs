// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Redirects the font download cache to a per-test-assembly directory so concurrent test processes never contend on the shared default cache.</summary>
internal static class FontCacheIsolation
{
    /// <summary>Points <c>NUSTREAMDOCS_FONTS_CACHE</c> at a directory keyed by this assembly's name, before any test runs.</summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        var assemblyName = typeof(FontCacheIsolation).Assembly.GetName().Name ?? "tests";
        Environment.SetEnvironmentVariable(
            "NUSTREAMDOCS_FONTS_CACHE",
            Path.Combine(Path.GetTempPath(), "nustreamdocs-fonts-cache", assemblyName));
    }
}
