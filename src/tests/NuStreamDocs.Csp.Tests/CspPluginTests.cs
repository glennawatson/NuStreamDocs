// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Csp.Tests;

/// <summary>Coverage for <see cref="CspPlugin"/>.</summary>
public class CspPluginTests
{
    /// <summary>A representative rendered page with a head and an inline body script.</summary>
    private const string Page = "<html><head><title>t</title></head><body><script>alert(1)</script><p>hi</p></body></html>";

    /// <summary>The plugin splices a <c>&lt;meta http-equiv="Content-Security-Policy"&gt;</c> before <c>&lt;/head&gt;</c>, with the page's inline script hashed in.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InjectsMetaBeforeHeadClose()
    {
        var plugin = new CspPlugin();
        await Assert.That(plugin.Name.SequenceEqual("csp"u8)).IsTrue();
        await Assert.That(plugin.NeedsRewrite(Encoding.UTF8.GetBytes(Page))).IsTrue();

        var output = Run(plugin, Page);
        var metaIdx = output.IndexOf("<meta http-equiv=\"Content-Security-Policy\" content=\"", StringComparison.Ordinal);
        await Assert.That(metaIdx).IsGreaterThan(0);
        await Assert.That(metaIdx).IsLessThan(output.IndexOf("</head>", StringComparison.Ordinal));
        var hash = "'sha256-" + Convert.ToBase64String(SHA256.HashData("alert(1)"u8)) + "'";
        await Assert.That(output).Contains("script-src 'self' " + hash);
        await Assert.That(output).Contains("default-src 'self'");
        await Assert.That(output).Contains("<p>hi</p>"); // body left intact
    }

    /// <summary>Report-only mode emits the <c>-Report-Only</c> header.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReportOnlyMode()
    {
        var output = Run(new CspPlugin(CspOptions.Default.WithReportOnly()), Page);
        await Assert.That(output).Contains("<meta http-equiv=\"Content-Security-Policy-Report-Only\" content=\"");
    }

    /// <summary>A page with no <c>&lt;/head&gt;</c>, or a disabled plugin, passes the HTML through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PassthroughCases()
    {
        await Assert.That(new CspPlugin().NeedsRewrite("<p>no head here</p>"u8)).IsFalse();
        await Assert.That(Run(new CspPlugin(), "<body><p>no head here</p></body>")).IsEqualTo("<body><p>no head here</p></body>");
        await Assert.That(new CspPlugin(CspOptions.Default.Disable()).NeedsRewrite(Encoding.UTF8.GetBytes(Page))).IsFalse();
        await Assert.That(Run(new CspPlugin(CspOptions.Default.Disable()), Page)).IsEqualTo(Page);
    }

    /// <summary>Runs the post-render rewrite and returns the output as a string.</summary>
    /// <param name="plugin">The plugin.</param>
    /// <param name="html">Input HTML.</param>
    /// <returns>The rewritten HTML.</returns>
    private static string Run(CspPlugin plugin, string html)
    {
        ArrayBufferWriter<byte> sink = new();
        PagePostRenderContext ctx = new("page.md", default, Encoding.UTF8.GetBytes(html), sink);
        plugin.PostRender(in ctx);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
