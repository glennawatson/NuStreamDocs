// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Layouts.Tests;

/// <summary>Tests for the <see cref="LayoutsOptionsExtensions"/> helpers.</summary>
public class LayoutsOptionsExtensionsTests
{
    /// <summary>WithTemplateDirectory copies the directory through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithTemplateDirectory_Sets()
    {
        var opts = LayoutsOptions.Default.WithTemplateDirectory("/tmp/x");
        await Assert.That(opts.TemplateDirectory.Value).IsEqualTo("/tmp/x");
    }

    /// <summary>WithMaxIncludeDepth copies the cap through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithMaxIncludeDepth_Sets()
    {
        var opts = LayoutsOptions.Default.WithMaxIncludeDepth(3);
        await Assert.That(opts.MaxIncludeDepth).IsEqualTo(3);
    }

    /// <summary>WithMaxIncludeDepth rejects zero.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithMaxIncludeDepth_RejectsZero() =>
        await Assert.That(() => LayoutsOptions.Default.WithMaxIncludeDepth(0))
            .Throws<ArgumentOutOfRangeException>();
}
