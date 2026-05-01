// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>Coverage for the LinkValidatorPlugin(opts, factory) constructor and ValidationCorpus.ContainsPage.</summary>
public class LinkValidatorPluginCtorTests
{
    /// <summary>Two-arg ctor accepts a custom HTTP client factory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoArgCtor()
    {
        var plugin = new LinkValidatorPlugin(LinkValidatorOptions.Default, httpClientFactory: () => new HttpClient());
        await Assert.That(plugin.Name).IsEqualTo("link-validator");
    }

    /// <summary>ValidationCorpus.ContainsPage returns false for unknown pages.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ContainsPageMiss()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smkd-vc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var corpus = await ValidationCorpus.BuildAsync(dir, parallelism: 1, CancellationToken.None);
            await Assert.That(corpus.ContainsPage("/missing.html")).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
