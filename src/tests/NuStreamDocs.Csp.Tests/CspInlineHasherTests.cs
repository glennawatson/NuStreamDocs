// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;

namespace NuStreamDocs.Csp.Tests;

/// <summary>Coverage for <see cref="CspInlineHasher"/>.</summary>
public class CspInlineHasherTests
{
    /// <summary>An inline script body is hashed into a <c>'sha256-…'</c> token; an empty / src-only script contributes nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HashesInlineScriptBodies()
    {
        byte[] html = [.. "<html><head></head><body><script src=\"/a.js\"></script><script>alert(1)</script><script></script></body></html>"u8];
        List<byte[]> hashes = [];
        CspInlineHasher.HashScripts(html, hashes);
        var expected = "'sha256-" + Convert.ToBase64String(SHA256.HashData("alert(1)"u8)) + "'";
        await Assert.That(hashes.Count).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(hashes[0])).IsEqualTo(expected);
    }

    /// <summary>An inline style body is hashed; the pre-filter spots inline blocks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HashesInlineStyleBodies()
    {
        byte[] html = [.. "<html><head><style>body{color:red}</style></head><body></body></html>"u8];
        byte[] noBlocks = [.. "<html><head></head><body></body></html>"u8];
        await Assert.That(CspInlineHasher.MayHaveInlineBlocks(html)).IsTrue();
        await Assert.That(CspInlineHasher.MayHaveInlineBlocks(noBlocks)).IsFalse();
        List<byte[]> hashes = [];
        CspInlineHasher.HashStyles(html, hashes);
        var expected = "'sha256-" + Convert.ToBase64String(SHA256.HashData("body{color:red}"u8)) + "'";
        await Assert.That(hashes.Count).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(hashes[0])).IsEqualTo(expected);
    }
}
