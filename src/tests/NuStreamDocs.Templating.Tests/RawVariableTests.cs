// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Templating.Tests;

/// <summary>Coverage for the <c>{{&amp;name}}</c> raw-variable instruction.</summary>
public class RawVariableTests
{
    /// <summary>Raw-variable syntax skips HTML-escaping the value.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RawVariableEmitsUnescaped()
    {
        var template = Template.Compile("hi {{&name}} bye"u8);
        var data = new TemplateData(
            new(Common.ByteArrayComparer.Instance)
            {
                ["name"u8.ToArray()] = "<b>X</b>"u8.ToArray(),
            },
            sections: null);
        var sink = new ArrayBufferWriter<byte>();
        template.Render(data, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("hi <b>X</b> bye");
    }
}
