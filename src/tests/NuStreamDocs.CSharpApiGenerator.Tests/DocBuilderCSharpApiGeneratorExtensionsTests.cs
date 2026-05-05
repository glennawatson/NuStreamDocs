// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Behavior tests for <c>DocBuilderCSharpApiGeneratorExtensions</c>.</summary>
public class DocBuilderCSharpApiGeneratorExtensionsTests
{
    /// <summary>The extension returns the same builder so the call chains.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnsSameBuilderForChaining()
    {
        DocBuilder builder = new();
        var options = CSharpApiGeneratorOptions.FromManifest("/repo", "/cache");
        var result = builder.UseCSharpApiGenerator(options);
        await Assert.That(ReferenceEquals(builder, result)).IsTrue();
    }

    /// <summary>The extension throws on a null options record.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RejectsNullOptions()
    {
        DocBuilder builder = new();
        await Assert.That(() => builder.UseCSharpApiGenerator(null!))
            .Throws<ArgumentNullException>();
    }
}
