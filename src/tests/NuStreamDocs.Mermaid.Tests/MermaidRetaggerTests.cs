// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Mermaid.Tests;

/// <summary>Behaviour tests for <c>MermaidRetagger</c>.</summary>
public class MermaidRetaggerTests
{
    /// <summary>HTML containing a fenced mermaid block is flagged for retagging.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NeedsRetagDetectsMermaidBlock()
    {
        byte[] html = [.. "<pre><code class=\"language-mermaid\">graph TD; A--&gt;B</code></pre>"u8];
        await Assert.That(MermaidRetagger.NeedsRetag(html)).IsTrue();
    }

    /// <summary>HTML without a mermaid block is not flagged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NeedsRetagSkipsOtherBlocks()
    {
        byte[] html = [.. "<pre><code class=\"language-csharp\">var x = 1;</code></pre>"u8];
        await Assert.That(MermaidRetagger.NeedsRetag(html)).IsFalse();
    }

    /// <summary>The retag swaps the wrapping <c>&lt;pre&gt;&lt;code&gt;</c> for the runtime-discoverable <c>&lt;pre class="mermaid"&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RetagSwapsWrapper()
    {
        const string Source = "before<pre><code class=\"language-mermaid\">graph TD; A-->B</code></pre>after";
        var rewritten = Encoding.UTF8.GetString(MermaidRetagger.Retag(Encoding.UTF8.GetBytes(Source)));
        await Assert.That(rewritten).IsEqualTo("before<pre class=\"mermaid\">graph TD; A-->B</pre>after");
    }

    /// <summary>Multiple mermaid blocks all get retagged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RetagHandlesMultipleBlocks()
    {
        const string Source = "<pre><code class=\"language-mermaid\">a</code></pre>x<pre><code class=\"language-mermaid\">b</code></pre>";
        var rewritten = Encoding.UTF8.GetString(MermaidRetagger.Retag(Encoding.UTF8.GetBytes(Source)));
        await Assert.That(rewritten).IsEqualTo("<pre class=\"mermaid\">a</pre>x<pre class=\"mermaid\">b</pre>");
    }

    /// <summary>HTML without any mermaid block is copied byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RetagWithoutOpenMarkerCopiesVerbatim()
    {
        const string Source = "<pre><code class=\"language-csharp\">var x = 1;</code></pre>";
        var rewritten = Encoding.UTF8.GetString(MermaidRetagger.Retag(Encoding.UTF8.GetBytes(Source)));
        await Assert.That(rewritten).IsEqualTo(Source);
    }

    /// <summary>An open marker without a closing <c>&lt;/code&gt;&lt;/pre&gt;</c> emits the original open marker plus the trailing bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RetagWithoutCloseMarkerLeavesOriginal()
    {
        const string Source = "before<pre><code class=\"language-mermaid\">unclosed body";
        var rewritten = Encoding.UTF8.GetString(MermaidRetagger.Retag(Encoding.UTF8.GetBytes(Source)));
        await Assert.That(rewritten).IsEqualTo(Source);
    }
}
