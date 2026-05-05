// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>
/// Heuristic language guesser for unlabeled <c>&lt;pre&gt;&lt;code&gt;</c> blocks.
/// </summary>
/// <remarks>
/// Scores each candidate language by counting hits of a small fixed signature table — distinctive
/// keywords or punctuation that strongly imply a particular language. The signatures are written
/// against the HTML-escaped form of the body bytes so callers don't need to unescape before
/// detection (saving an allocation per block on the no-confidence path).
/// <para>
/// A match is reported only when the winning score crosses an absolute floor and beats the
/// runner-up by a comfortable margin — <see cref="HighlightOptions.AutoDetectLanguage"/>'s docs
/// promise misclassification-resistance, not coverage. Below threshold the block stays unlabeled
/// and the highlighter passes it through verbatim.
/// </para>
/// </remarks>
internal static class LanguageDetector
{
    /// <summary>Maximum body bytes inspected. Caps worst-case cost on pathological multi-MB code dumps; every detectable language is identifiable from the first few hundred bytes.</summary>
    private const int ScanLimit = 2048;

    /// <summary>Minimum score required for a match to be reported.</summary>
    private const int MinScore = 3;

    /// <summary>Winning score must beat the runner-up by at least this multiplier (rounded).</summary>
    private const int LeaderRunnerUpRatio = 2;

    /// <summary>Static profile table — appended to as new lexers gain signatures. Keep in sync with <see cref="LexerRegistry"/>'s built-in aliases.</summary>
    private static readonly LanguageProfile[] Profiles =
    [
        new(
            [.. "csharp"u8],
            [
                new([.. "using System"u8], 2),
                new([.. "namespace "u8], 2),
                new([.. "public class"u8], 2),
                new([.. "public static"u8], 1),
                new([.. "private "u8], 1),
                new([.. "ReactiveObject"u8], 2),
                new([.. "this."u8], 1),
                new([.. "Console.Write"u8], 1),
                new([.. "=&gt; "u8], 1),
                new([.. "string "u8], 1),
                new([.. "void "u8], 1)
            ]),
        new(
            [.. "powershell"u8],
            [
                new([.. "Install-Package"u8], 3),
                new([.. "Get-"u8], 2),
                new([.. "Set-"u8], 1),
                new([.. "Write-Host"u8], 2),
                new([.. "Where-Object"u8], 2),
                new([.. "ForEach-"u8], 2),
                new([.. "$_"u8], 1),
                new([.. "Param("u8], 1)
            ]),
        new(
            [.. "xml"u8],
            [
                new([.. "&lt;?xml"u8], 2),
                new([.. "xmlns"u8], 2),
                new([.. "&lt;/"u8], 1),
                new([.. "&gt;&lt;"u8], 1)
            ]),
        new(
            [.. "html"u8],
            [
                new([.. "&lt;!DOCTYPE"u8], 2),
                new([.. "&lt;html"u8], 2),
                new([.. "&lt;head"u8], 1),
                new([.. "&lt;body"u8], 1),
                new([.. "&lt;div"u8], 1),
                new([.. "&lt;span"u8], 1)
            ]),
        new(
            [.. "json"u8],
            [
                new([.. "&quot;:&quot;"u8], 2),
                new([.. "&quot;: &quot;"u8], 2),
                new([.. "&quot;: ["u8], 1),
                new([.. "&quot;: {"u8], 1),
                new([.. "&quot;: "u8], 1)
            ]),
        new(
            [.. "typescript"u8],
            [
                new([.. "interface "u8], 2),
                new([.. ": string"u8], 2),
                new([.. ": number"u8], 2),
                new([.. ": boolean"u8], 2),
                new([.. "as const"u8], 2),
                new([.. "export type"u8], 2)
            ]),
        new(
            [.. "javascript"u8],
            [
                new([.. "const "u8], 1),
                new([.. "function "u8], 1),
                new([.. "console.log"u8], 2),
                new([.. "=&gt; {"u8], 1),
                new([.. "let "u8], 1),
                new([.. "require("u8], 1),
                new([.. "module.exports"u8], 2)
            ]),
        new(
            [.. "python"u8],
            [
                new([.. "def "u8], 2),
                new([.. "import "u8], 1),
                new([.. "from "u8], 1),
                new([.. "self."u8], 2),
                new([.. "    pass"u8], 1),
                new([.. "if __name__"u8], 2),
                new([.. "print("u8], 1)
            ]),
        new(
            [.. "bash"u8],
            [
                new([.. "#!/bin/sh"u8], 2),
                new([.. "#!/bin/bash"u8], 2),
                new([.. "#!/usr/bin/env bash"u8], 2),
                new([.. "echo "u8], 1),
                new([.. "$(which"u8], 1),
                new([.. "if [ "u8], 2),
                new([.. "fi\n"u8], 1)
            ]),
        new(
            [.. "yaml"u8],
            [
                new([.. "---\n"u8], 1),
                new([.. ":\n  "u8], 1),
                new([.. "- name:"u8], 2),
                new([.. "version: "u8], 1)
            ]),
        new(
            [.. "fsharp"u8],
            [
                new([.. "let "u8], 1),
                new([.. "module "u8], 2),
                new([.. " |&gt; "u8], 2),
                new([.. "open System"u8], 2),
                new([.. "match "u8], 1)
            ]),
        new(
            [.. "diff"u8],
            [
                new([.. "\n+++ "u8], 2),
                new([.. "\n--- "u8], 2),
                new([.. "\n@@ "u8], 2)
            ])
    ];

    /// <summary>
    /// Tries to identify the language of <paramref name="escapedBody"/>; only languages with a registered
    /// lexer in <paramref name="registry"/> (and on the optional <paramref name="allowList"/>) are considered.
    /// </summary>
    /// <param name="escapedBody">HTML-escaped UTF-8 body bytes (the slice between <c>&gt;</c> and <c>&lt;/code&gt;</c>).</param>
    /// <param name="registry">Active lexer registry — restricts candidates to languages the highlighter can actually colour.</param>
    /// <param name="allowList">
    /// Optional caller-declared language alias allow-list. Empty array means "every registered language is in
    /// scope" (the default). Each entry is a lowercased UTF-8 alias matching the <see cref="LexerRegistry"/>
    /// aliases (e.g. <c>"csharp"u8</c>).
    /// </param>
    /// <param name="languageId">On success, the lowercased alias bytes; tied to a static buffer (do not mutate).</param>
    /// <returns>True when a confident match was found.</returns>
    public static bool TryDetect(ReadOnlySpan<byte> escapedBody, LexerRegistry registry, byte[][] allowList, out ReadOnlySpan<byte> languageId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(allowList);
        languageId = default;

        var scan = escapedBody.Length > ScanLimit ? escapedBody[..ScanLimit] : escapedBody;
        if (scan.IsEmpty)
        {
            return false;
        }

        var bestScore = 0;
        var runnerUpScore = 0;
        var bestIndex = -1;

        for (var i = 0; i < Profiles.Length; i++)
        {
            var profile = Profiles[i];
            if (!IsCandidate(profile.LanguageId, registry, allowList))
            {
                continue;
            }

            var score = ScoreProfile(scan, in profile);
            if (score > bestScore)
            {
                runnerUpScore = bestScore;
                bestScore = score;
                bestIndex = i;
                continue;
            }

            if (score > runnerUpScore)
            {
                runnerUpScore = score;
            }
        }

        if (bestIndex < 0 || bestScore < MinScore)
        {
            return false;
        }

        if (bestScore < runnerUpScore * LeaderRunnerUpRatio)
        {
            return false;
        }

        languageId = Profiles[bestIndex].LanguageId;
        return true;
    }

    /// <summary>Sums weighted hits for one language profile against the scan window.</summary>
    /// <param name="scan">Capped body slice.</param>
    /// <param name="profile">Candidate signature row.</param>
    /// <returns>Total score; one increment per signature hit.</returns>
    private static int ScoreProfile(ReadOnlySpan<byte> scan, in LanguageProfile profile)
    {
        var total = 0;
        var signatures = profile.Signatures;
        for (var i = 0; i < signatures.Length; i++)
        {
            if (scan.IndexOf(signatures[i].Keyword) >= 0)
            {
                total += signatures[i].Weight;
            }
        }

        return total;
    }

    /// <summary>True when <paramref name="languageId"/> can be considered: a lexer is registered AND the optional allow-list either is empty or contains the alias.</summary>
    /// <param name="languageId">Profile language alias.</param>
    /// <param name="registry">Active lexer registry.</param>
    /// <param name="allowList">Caller-declared allow-list; empty means "no restriction".</param>
    /// <returns>True when the profile is in scope.</returns>
    private static bool IsCandidate(byte[] languageId, LexerRegistry registry, byte[][] allowList)
    {
        if (!registry.TryGet(languageId, out _))
        {
            return false;
        }

        if (allowList.Length is 0)
        {
            return true;
        }

        for (var i = 0; i < allowList.Length; i++)
        {
            if (allowList[i].AsSpan().SequenceEqual(languageId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Keyword and weight for a single language signature row.</summary>
    /// <param name="Keyword">UTF-8 bytes the body is searched for. Distinctive substrings of the language; keep short and language-specific.</param>
    /// <param name="Weight">Hit weight; <c>1</c> for "fairly common in this language", <c>2</c> for "almost-exclusive marker".</param>
    private readonly record struct Signature(byte[] Keyword, int Weight);

    /// <summary>One language candidate the detector considers.</summary>
    /// <param name="LanguageId">Lowercased UTF-8 alias the highlighter dispatches on (matches the <see cref="LexerRegistry"/> alias).</param>
    /// <param name="Signatures">Distinctive byte-keyword set; hits sum to the language's score.</param>
    private readonly record struct LanguageProfile(byte[] LanguageId, Signature[] Signatures);
}
