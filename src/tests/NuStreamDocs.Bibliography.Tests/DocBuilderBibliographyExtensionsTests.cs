// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NSubstitute;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Bibliography.Styles;
using NuStreamDocs.Building;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Tests for the <see cref="DocBuilderBibliographyExtensions"/> helpers.</summary>
public class DocBuilderBibliographyExtensionsTests
{
    /// <summary>UseBibliography registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseBibliography_registers_plugin()
    {
        DocBuilder builder = new();
        var result = builder.UseBibliography(BibliographyOptions.Default);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>UseBibliography with explicit style and database callback registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseBibliography_with_style_and_callback_registers_plugin()
    {
        DocBuilder builder = new();
        var style = Substitute.For<ICitationStyle>();
        var result = builder.UseBibliography(style, static db => db.Add(new() { Id = [.. "key"u8], Type = EntryType.Book, Title = [.. "title"u8] }));
        await Assert.That(result).IsSameReferenceAs(builder);
    }
}
