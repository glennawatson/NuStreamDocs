// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Tests;

/// <summary>Tests for <c>BuildPipelineOptions</c>.</summary>
public class BuildPipelineOptionsTests
{
    /// <summary>Default options use empty filter, no logger, flat URLs, drafts skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultIsEmpty()
    {
        var defaults = BuildPipelineOptions.Default;
        await Assert.That(defaults.Filter).IsSameReferenceAs(PathFilter.Empty);
        await Assert.That(defaults.Logger).IsNull();
        await Assert.That(defaults.UseDirectoryUrls).IsFalse();
        await Assert.That(defaults.IncludeDrafts).IsFalse();
    }

    /// <summary>Record-with semantics produce a derived options struct.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RecordWithSemantics()
    {
        var derived = BuildPipelineOptions.Default with { UseDirectoryUrls = true, IncludeDrafts = true };
        await Assert.That(derived.UseDirectoryUrls).IsTrue();
        await Assert.That(derived.IncludeDrafts).IsTrue();
    }
}
