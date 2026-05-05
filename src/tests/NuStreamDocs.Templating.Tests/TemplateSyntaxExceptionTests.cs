// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Templating.Tests;

/// <summary>Constructor coverage for TemplateSyntaxException.</summary>
public class TemplateSyntaxExceptionTests
{
    /// <summary>Default ctor produces a non-null instance.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultCtor()
    {
        TemplateSyntaxException ex = new();
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Message ctor preserves the message.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MessageCtor()
    {
        TemplateSyntaxException ex = new("oops");
        await Assert.That(ex.Message).IsEqualTo("oops");
    }

    /// <summary>Message + inner ctor preserves both.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MessageInnerCtor()
    {
        InvalidOperationException inner = new("inner");
        TemplateSyntaxException ex = new("oops", inner);
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }

    /// <summary>Message + offset ctor records the byte offset.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MessageOffsetCtor()
    {
        TemplateSyntaxException ex = new("oops", 42);
        await Assert.That(ex.ByteOffset).IsEqualTo(42);
    }

    /// <summary>Message + offset + inner ctor records the byte offset and inner exception.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MessageOffsetInnerCtor()
    {
        InvalidOperationException inner = new("inner");
        TemplateSyntaxException ex = new("oops", 7, inner);
        await Assert.That(ex.ByteOffset).IsEqualTo(7);
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }
}
