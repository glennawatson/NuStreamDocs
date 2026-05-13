// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Audit.Tests;

/// <summary>Coverage for <see cref="PageAuditor"/> and the individual lint modules.</summary>
public class PageAuditorTests
{
    /// <summary>Head markup that satisfies the document-structure lints.</summary>
    private const string GoodHead =
        "<head><title>t</title><meta name=\"viewport\" content=\"width=device-width\"></head>";

    /// <summary>A well-formed page raises no findings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CleanPageHasNoFindings()
    {
        const string Html = "<!DOCTYPE html><html lang=\"en\">"
                            + "<head><title>Hi</title><meta name=\"viewport\" content=\"x\"><script src=\"/app.js\" defer></script></head>"
                            + "<body><h1>Title</h1><h2>Sub</h2><img src=\"a.png\" alt=\"A\" width=\"10\" height=\"10\"><a href=\"/x\">link text</a></body></html>";
        await Assert.That(RulesFor(Html)).IsEmpty();
    }

    /// <summary>An image without alt or dimensions raises both image lints.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ImageWithoutAltOrDimensions()
    {
        var rules = RulesFor(Wrap("<img src=\"a.png\">"));
        await Assert.That(rules).Contains(AuditRule.ImageMissingAlt);
        await Assert.That(rules).Contains(AuditRule.ImageMissingDimensions);
    }

    /// <summary>An explicit empty alt and an aspect-ratio style suppress the image lints.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DecorativeImageWithAspectRatioIsClean()
    {
        var rules = RulesFor(Wrap("<img src=\"a.png\" alt=\"\" style=\"aspect-ratio: 16/9\">"));
        await Assert.That(rules).DoesNotContain(AuditRule.ImageMissingAlt);
        await Assert.That(rules).DoesNotContain(AuditRule.ImageMissingDimensions);
    }

    /// <summary>A skipped heading level and a missing h1 are both flagged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeadingOutlineProblems()
    {
        await Assert.That(RulesFor(WrapBody("<h1>a</h1><h3>b</h3>"))).Contains(AuditRule.HeadingLevelSkipped);
        await Assert.That(RulesFor(WrapBody("<h2>a</h2><h3>b</h3>"))).Contains(AuditRule.HeadingMissingH1);
        await Assert.That(RulesFor(WrapBody("<h1>a</h1><h1>b</h1>"))).Contains(AuditRule.HeadingMultipleH1);
    }

    /// <summary>Missing document landmarks are flagged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingDocumentLandmarks()
    {
        var rules = RulesFor("<html><head><title>  </title></head><body><h1>h</h1></body></html>");
        await Assert.That(rules).Contains(AuditRule.HtmlMissingLang);
        await Assert.That(rules).Contains(AuditRule.DocumentMissingTitle);
        await Assert.That(rules).Contains(AuditRule.DocumentMissingViewport);
    }

    /// <summary>An icon-only link with no accessible name is flagged; an aria-labeled one is not.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyAndLabeledLinks()
    {
        await Assert.That(RulesFor(Wrap("<a href=\"/x\"><i class=\"icon-home\"></i></a>")))
            .Contains(AuditRule.EmptyLink);
        await Assert.That(RulesFor(Wrap("<a href=\"/x\" aria-label=\"Home\"><i class=\"icon-home\"></i></a>")))
            .DoesNotContain(AuditRule.EmptyLink);
    }

    /// <summary>An empty button is flagged; one with text is not.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyAndTextButtons()
    {
        await Assert.That(RulesFor(Wrap("<button><span class=\"x\"></span></button>"))).Contains(AuditRule.EmptyButton);
        await Assert.That(RulesFor(Wrap("<button>Go</button>"))).DoesNotContain(AuditRule.EmptyButton);
    }

    /// <summary>A positive tabindex is flagged; zero is not.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PositiveTabIndex()
    {
        await Assert.That(RulesFor(Wrap("<div tabindex=\"3\">x</div>"))).Contains(AuditRule.PositiveTabIndex);
        await Assert.That(RulesFor(Wrap("<div tabindex=\"0\">x</div>"))).DoesNotContain(AuditRule.PositiveTabIndex);
    }

    /// <summary>An unlabeled input is flagged; a wrapped, for-linked, or hidden one is not.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FormControlLabels()
    {
        await Assert.That(RulesFor(Wrap("<input type=\"text\" id=\"q\">"))).Contains(AuditRule.UnlabeledFormControl);
        await Assert.That(RulesFor(Wrap("<label for=\"q\">Query</label><input type=\"text\" id=\"q\">")))
            .DoesNotContain(AuditRule.UnlabeledFormControl);
        await Assert.That(RulesFor(Wrap("<label>Query <input type=\"text\"></label>")))
            .DoesNotContain(AuditRule.UnlabeledFormControl);
        await Assert.That(RulesFor(Wrap("<input type=\"hidden\" name=\"q\">")))
            .DoesNotContain(AuditRule.UnlabeledFormControl);
    }

    /// <summary>A render-blocking head script is flagged; async/defer/module are not.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RenderBlockingScript()
    {
        const string Blocking =
            "<html lang=\"en\"><head><title>t</title><meta name=\"viewport\" content=\"x\"><script src=\"/a.js\"></script></head><body><h1>h</h1></body></html>";
        await Assert.That(RulesFor(Blocking)).Contains(AuditRule.RenderBlockingScript);

        const string Module =
            "<html lang=\"en\"><head><title>t</title><meta name=\"viewport\" content=\"x\"><script src=\"/a.js\" type=\"module\"></script></head><body><h1>h</h1></body></html>";
        await Assert.That(RulesFor(Module)).DoesNotContain(AuditRule.RenderBlockingScript);

        await Assert.That(RulesFor(Wrap("<script src=\"/a.js\"></script>")))
            .DoesNotContain(AuditRule.RenderBlockingScript);
    }

    /// <summary>A meta-refresh redirect stub produces no findings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RedirectStubIsSkipped()
    {
        var rules = RulesFor(
            "<html><head><meta http-equiv=\"refresh\" content=\"0; url=/new/\"></head><body></body></html>");
        await Assert.That(rules).IsEmpty();
    }

    /// <summary>A disabled rule does not fire.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DisabledRuleIsSkipped()
    {
        var options = AuditOptions.Default.Disable(AuditRule.ImageMissingAlt);
        var diagnostics = PageAuditor.Audit(
            "page.html",
            Encoding.UTF8.GetBytes(Wrap("<img src=\"a.png\" width=\"1\" height=\"1\">")),
            options);
        await Assert.That(diagnostics.Select(d => d.Rule)).DoesNotContain(AuditRule.ImageMissingAlt);
    }

    /// <summary>Wraps a body fragment in a clean document shell containing a single <c>&lt;h1&gt;</c>.</summary>
    /// <param name="bodyFragment">Markup to place after the heading.</param>
    /// <returns>A complete HTML document.</returns>
    private static string Wrap(string bodyFragment) =>
        "<html lang=\"en\">" + GoodHead + "<body><h1>h</h1>" + bodyFragment + "</body></html>";

    /// <summary>Wraps body markup (including its own headings) in a clean document shell.</summary>
    /// <param name="body">Body markup.</param>
    /// <returns>A complete HTML document.</returns>
    private static string WrapBody(string body) =>
        "<html lang=\"en\">" + GoodHead + "<body>" + body + "</body></html>";

    /// <summary>Audits a page and returns the rules that fired.</summary>
    /// <param name="html">Page HTML.</param>
    /// <returns>The set of fired rules.</returns>
    private static AuditRule[] RulesFor(string html)
    {
        var diagnostics = PageAuditor.Audit("page.html", Encoding.UTF8.GetBytes(html), AuditOptions.Default);
        return [.. diagnostics.Select(d => d.Rule)];
    }
}
