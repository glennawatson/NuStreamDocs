// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Compiled glob-pattern matcher for URL-level allow/exclude rules.
/// Supports <c>*</c> (any chars) and <c>?</c> (one char) wildcards;
/// every other character is matched literally.
/// </summary>
internal sealed class UrlPatternMatcher
{
    /// <summary>Per-match upper bound — every URL pattern is bounded so a pathological input can't lock the build.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Pre-compiled patterns; empty matcher matches nothing.</summary>
    private readonly Regex[] _patterns;

    /// <summary>Initializes a new instance of the <see cref="UrlPatternMatcher"/> class.</summary>
    /// <param name="patterns">Glob patterns (case-insensitive). Null or empty disables matching.</param>
    public UrlPatternMatcher(string[]? patterns) => _patterns = CompileAll(patterns);

    /// <summary>Gets a value indicating whether any patterns are configured.</summary>
    public bool HasPatterns => _patterns is [_, ..];

    /// <summary>Returns true when at least one configured pattern matches <paramref name="url"/>.</summary>
    /// <param name="url">Candidate URL.</param>
    /// <returns>True on match.</returns>
    public bool IsMatch(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        for (var i = 0; i < _patterns.Length; i++)
        {
            if (_patterns[i].IsMatch(url))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Compiles one glob pattern into a regex anchored at both ends.</summary>
    /// <param name="glob">Glob pattern.</param>
    /// <returns>Compiled, case-insensitive <see cref="Regex"/>.</returns>
    private static Regex Compile(string glob)
    {
        ArgumentException.ThrowIfNullOrEmpty(glob);
        Span<char> single = stackalloc char[1];
        var sb = new StringBuilder(glob.Length + 4).Append('^');
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            switch (c)
            {
                case '*':
                {
                    sb.Append(".*");
                    break;
                }

                case '?':
                {
                    sb.Append('.');
                    break;
                }

                default:
                {
                    single[0] = c;
                    sb.Append(Regex.Escape(new(single)));
                    break;
                }
            }
        }

        sb.Append('$');
        return new(sb.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);
    }

    /// <summary>Compiles every supplied pattern into a right-sized array.</summary>
    /// <param name="patterns">Optional pattern sequence.</param>
    /// <returns>Compiled pattern array.</returns>
    private static Regex[] CompileAll(string[]? patterns)
    {
        if (patterns is null or [])
        {
            return [];
        }

        var compiled = new Regex[patterns.Length];
        for (var i = 0; i < patterns.Length; i++)
        {
            compiled[i] = Compile(patterns[i]);
        }

        return compiled;
    }
}
