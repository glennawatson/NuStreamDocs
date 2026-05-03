// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Caching;

namespace NuStreamDocs.Tests;

/// <summary>Tests for the <see cref="ContentHasher"/>.</summary>
public class ContentHasherTests
{
    /// <summary>HashFileAsync computes a hash for a file on disk.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HashFileAsync_computes_hash()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "content");
            var hash = await ContentHasher.HashFileAsync(path, CancellationToken.None);
            await Assert.That(hash.Length).IsEqualTo(ContentHasher.HashByteLength);

            var hash2 = await ContentHasher.HashFileAsync(path, CancellationToken.None);
            await Assert.That(hash.AsSpan().SequenceEqual(hash2)).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
