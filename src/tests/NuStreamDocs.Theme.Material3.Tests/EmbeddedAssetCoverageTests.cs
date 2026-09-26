// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Coverage for Material3 EmbeddedAsset.ToResourceName.</summary>
public class EmbeddedAssetCoverageTests
{
    /// <summary>Embedded path of the bundled stylesheet.</summary>
    private const string StylesheetPath = "assets/stylesheets/material3.css";

    /// <summary>ToResourceName produces a non-empty resource path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToResourceNameProducesPath()
    {
        var name = EmbeddedAsset.ToResourceName("css/site.css");
        await Assert.That(name).IsNotNull();
        await Assert.That(name.Length).IsGreaterThan(0);
    }

    /// <summary>Hand-authored stylesheet asset can be read from the embedded-resource table.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BundledStylesheetCanBeRead()
    {
        var bytes = EmbeddedAsset.ReadBytes(StylesheetPath);
        await Assert.That(bytes.Length).IsGreaterThan(0);
    }

    /// <summary>
    /// The bundled stylesheet ships <c>.md-clipboard</c> rules with a hover-fade transition;
    /// without these the copy button emitted by <c>HighlightPlugin</c> would be invisible.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BundledStylesheetIncludesClipboardRules()
    {
        var css = Encoding.UTF8.GetString(EmbeddedAsset.ReadBytes(StylesheetPath));
        await Assert.That(css).Contains(".md-clipboard");
        await Assert.That(css).Contains("--md-clipboard-icon");
        await Assert.That(css).Contains(".md-clipboard--copied");
    }

    /// <summary>
    /// The bundled stylesheet sizes mermaid diagrams as full-width figures in the text font; without these
    /// rules the inline-icon sizing shrinks the diagram to 1.125em and the code font clips its labels.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BundledStylesheetIncludesMermaidRules()
    {
        var css = Encoding.UTF8.GetString(EmbeddedAsset.ReadBytes(StylesheetPath));
        await Assert.That(css).Contains(".md-typeset pre.mermaid {\n    font-family: inherit;");
        await Assert.That(css).Contains(".md-typeset .mermaid svg[viewBox] {");
        await Assert.That(css).Contains("    width: 100%;\n    height: auto;");
    }

    /// <summary>
    /// The bundled JS ships a click handler that wires the <c>.md-clipboard</c> button to the
    /// <c>navigator.clipboard.writeText</c> API; without it the button is decorative only.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BundledJavascriptIncludesClipboardHandler()
    {
        var js = Encoding.UTF8.GetString(EmbeddedAsset.ReadBytes("assets/javascripts/material3.js"));
        await Assert.That(js).Contains(".md-clipboard");
        await Assert.That(js).Contains("navigator.clipboard");
        await Assert.That(js).Contains("md-clipboard--copied");
    }
}
