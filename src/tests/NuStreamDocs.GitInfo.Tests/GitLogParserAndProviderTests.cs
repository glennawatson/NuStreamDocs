// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.GitInfo.Tests;

/// <summary>Unit tests for <see cref="GitLogParser"/> and <see cref="ProcessGitLogProvider"/>.</summary>
public class GitLogParserAndProviderTests
{
    /// <summary>Shared guide page path.</summary>
    private const string GuideIntroPath = "guide/intro.md";

    /// <summary>Shared index page path.</summary>
    private const string IndexFileName = "index.md";

    /// <summary>Batched <c>git log</c> parser deduplicates authors, respects contributor caps, and skips malformed records.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ParseLogAndTryResolvePageCoverAllBranches()
    {
        const int CapTwo = 2;
        const int CapOne = 1;
        var rawLog = "noise\u001e2026-11-28\u001fAlice\n\nguide/intro.md\n\u001ebadheader\nguide/intro.md\n\u001e\u001fNoDate\nguide/intro.md\n\u001e2026-11-20\u001f\nguide/intro.md\n\u001e2026-11-10\u001fAlice\nguide/intro.md\n\u001e2026-11-05\u001fBob\nguide/intro.md\n\u001e2026-11-01\u001fCharlie\nguide/intro.md\n"u8;

        var map = GitLogParser.ParseLog(rawLog, CapTwo);
        var noSepMap = GitLogParser.ParseLog("plain-bytes-without-record-sep"u8, CapTwo);

        var emptyResolved = GitLogParser.TryResolvePage(default, "# Hi"u8, map, CapTwo, out _);
        var optedOut = GitLogParser.TryResolvePage(GuideIntroPath, "---\ngit_info: false\n---\n"u8, map, CapTwo, out _);
        var unknownNoFront = GitLogParser.TryResolvePage("missing.md", "# No date"u8, map, CapTwo, out _);
        var gitOnlyTrimmed = GitLogParser.TryResolvePage("././guide\\intro.md", "---\ngit_info: true\n---\n"u8, map, CapOne, out var trimmedMeta);
        var gitOnlyFull = GitLogParser.TryResolvePage(GuideIntroPath, "# Intro"u8, map, CapTwo, out var fullMeta);
        var frontUpdated = GitLogParser.TryResolvePage(GuideIntroPath, "---\nlast_updated: 2026-12-01\nauthor: Alice\n---\n"u8, map, CapTwo, out var mergedDupMeta);
        var frontDateNewAuthor = GitLogParser.TryResolvePage("missing.md", "---\ndate: 2026-10-01\nauthor: Zoe\n---\n"u8, map, CapTwo, out var frontOnlyMeta);

        await Assert.That(noSepMap.Count).IsEqualTo(0);
        await Assert.That(emptyResolved || optedOut || unknownNoFront).IsFalse();
        await Assert.That(gitOnlyTrimmed && gitOnlyFull && frontUpdated && frontDateNewAuthor).IsTrue();
        await Assert.That(trimmedMeta.Contributors.Length).IsEqualTo(CapOne);
        await Assert.That(fullMeta.Contributors.Length).IsEqualTo(CapTwo);
        await Assert.That(Encoding.UTF8.GetString(mergedDupMeta.LastUpdatedIsoDate)).IsEqualTo("2026-12-01");
        await Assert.That(mergedDupMeta.Contributors.Length).IsEqualTo(CapTwo);
        await Assert.That(Encoding.UTF8.GetString(frontOnlyMeta.LastUpdatedIsoDate)).IsEqualTo("2026-10-01");
    }

    /// <summary><see cref="ProcessGitLogProvider"/> handles real Git repositories, non-Git directories, missing paths, and missing binaries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ProcessGitLogProviderCoversEmptyNonGitMissingBinaryAndRealRepo()
    {
        DirectoryPath tempRoot = Path.Combine(Path.GetTempPath(), $"nd-gitprov-{Guid.NewGuid():N}");
        DirectoryPath missingDir = Path.Combine(Path.GetTempPath(), $"nd-gitmissing-{Guid.NewGuid():N}");

        try
        {
            _ = tempRoot.Create();
            var emptyResult = await ProcessGitLogProvider.Instance.ReadGitLogAsync(default, CancellationToken.None);
            var missingDirResult = await ProcessGitLogProvider.Instance.ReadGitLogAsync(missingDir, CancellationToken.None);
            var nonGitResult = await ProcessGitLogProvider.Instance.ReadGitLogAsync(tempRoot, CancellationToken.None);

            ProcessGitLogProvider badBinary = new("nd-nonexistent-git-binary-xyz");
            var missingBinaryResult = await badBinary.ReadGitLogAsync(tempRoot, CancellationToken.None);

            await RunGitAsync(tempRoot, "init");
            await RunGitAsync(tempRoot, "config", "user.name", "Test Author");
            await RunGitAsync(tempRoot, "config", "user.email", "author@example.test");
            await tempRoot.File(IndexFileName).WriteAllBytesAsync("# Home\n"u8.ToArray());
            await RunGitAsync(tempRoot, "add", IndexFileName);
            await RunGitAsync(tempRoot, "commit", "-m", "init docs");

            var realRepoLog = await ProcessGitLogProvider.Instance.ReadGitLogAsync(tempRoot, CancellationToken.None);
            var parsed = GitLogParser.ParseLog(realRepoLog, GitInfoOptions.DefaultMaxContributors);

            await Assert.That(emptyResult.Length + missingDirResult.Length + nonGitResult.Length + missingBinaryResult.Length).IsEqualTo(0);
            await Assert.That(parsed.ContainsKey("index.md"u8.ToArray())).IsTrue();
        }
        finally
        {
            if (tempRoot.Exists())
            {
                tempRoot.DeleteRecursive(true);
            }
        }
    }

    /// <summary>Executes a <c>git</c> command inside <paramref name="workingDir"/>.</summary>
    /// <param name="workingDir">Repository directory.</param>
    /// <param name="arguments">Git CLI arguments.</param>
    /// <returns>Completion task.</returns>
    private static async Task RunGitAsync(DirectoryPath workingDir, params string[] arguments)
    {
        ProcessStartInfo info = new("git") { WorkingDirectory = workingDir.Value, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        for (var i = 0; i < arguments.Length; i++)
        {
            info.ArgumentList.Add(arguments[i]);
        }

        using var proc = Process.Start(info)!;
        await proc.WaitForExitAsync();
    }
}
