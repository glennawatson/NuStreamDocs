// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Coverage for DocBuilderPrivacyExtensions.UsePrivacy(Func, logger).</summary>
public class DocBuilderPrivacyExtensionsCoverageTests
{
    /// <summary>UsePrivacy(builder, configure, logger) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UsePrivacyConfigureLogger()
    {
        var b = new DocBuilder().UsePrivacy(o => o, NullLogger.Instance);
        await Assert.That(b).IsNotNull();
    }
}
