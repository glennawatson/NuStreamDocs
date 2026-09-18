// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>Provides isolated paths for process tests without an executable dependency.</summary>
internal sealed class PagefindTestFiles : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="PagefindTestFiles"/> class.</summary>
    internal PagefindTestFiles()
    {
        Root = Path.Combine(Path.GetTempPath(), $"smkd-pf-cli-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Root);
        Binary = Path.Combine(Root, "pagefind");
        File.WriteAllBytes(Binary, []);
    }

    /// <summary>Gets the isolated site directory.</summary>
    internal string Root { get; }

    /// <summary>Gets the binary-path placeholder used during resolution.</summary>
    internal FilePath Binary { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Directory.Delete(Root, true);
}
