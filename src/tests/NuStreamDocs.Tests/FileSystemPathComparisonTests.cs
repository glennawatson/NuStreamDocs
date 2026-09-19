// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Path comparisons follow the target filesystem rather than an operating-system guess.</summary>
public sealed class FileSystemPathComparisonTests
{
    /// <summary>The selected standard comparer agrees with actual filename lookup and leaves no probe files.</summary>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task SelectsFilesystemCaseSensitivityAndCleansUp()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "CaseProbe.txt");
            await File.WriteAllTextAsync(file, "preserve this file");
            var ignoresCase = File.Exists(Path.Combine(directory.FullName, "caseprobe.txt"));

            var comparer = FileSystemPathComparison.GetComparer(directory.FullName);

            await Assert.That(comparer.Equals("CaseProbe.txt", "caseprobe.txt")).IsEqualTo(ignoresCase);
            await Assert.That(ReferenceEquals(comparer, ignoresCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)).IsTrue();
            await Assert.That(Directory.GetFiles(directory.FullName)).HasSingleItem();
            await Assert.That(await File.ReadAllTextAsync(file)).IsEqualTo("preserve this file");
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
