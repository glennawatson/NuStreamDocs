// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace NuStreamDocs.Highlight;

/// <summary>
/// A compiled Pygments-shape state-machine lexer.
/// </summary>
/// <remarks>
/// A lexer is a frozen map from state name to ordered <see cref="LexerRule"/>
/// list. <see cref="Tokenise(string, TokenSink)"/> walks the input once,
/// advancing the cursor past the longest rule that matches at each position;
/// on no match the cursor advances by one character with the
/// <see cref="TokenClass.Text"/> classification — guarantees forward progress
/// and never throws.
/// <para>
/// Built-in lexers compile every pattern via <c>[GeneratedRegex]</c>
/// partial methods, so the lexer instance is reused across every fenced
/// block on every page without per-call allocation. The hot path uses
/// <see cref="System.Text.RegularExpressions.Regex.EnumerateMatches(ReadOnlySpan{char})"/>
/// instead of <c>Regex.Match</c> to avoid the per-token <c>Match</c>-object
/// allocation, and the per-call state stack is rented from a thread-static
/// cache so a typical call allocates nothing.
/// </para>
/// </remarks>
public sealed class Lexer
{
    /// <summary>Initial capacity for the rented state stack; most languages stay shallow.</summary>
    private const int StateStackInitialCapacity = 8;

    /// <summary>Cap above which a parked stack is dropped instead of cached, so a pathological corpus doesn't pin a worker's stack forever.</summary>
    private const int StateStackMaxCachedCapacity = 64;

    /// <summary>Per-thread parked state stack reused across <see cref="Tokenise(string, TokenSink)"/> calls on the same worker.</summary>
    [ThreadStatic]
    private static Stack<string>? _stateStackCache;

    /// <summary>Initializes a new instance of the <see cref="Lexer"/> class.</summary>
    /// <param name="languageName">Display / matching name (e.g. <c>csharp</c>).</param>
    /// <param name="states">State name → ordered rule list.</param>
    public Lexer(string languageName, FrozenDictionary<string, LexerRule[]> states)
    {
        ArgumentException.ThrowIfNullOrEmpty(languageName);
        ArgumentNullException.ThrowIfNull(states);
        if (!states.ContainsKey(RootState))
        {
            throw new ArgumentException($"Lexer state map for '{languageName}' must contain a '{RootState}' state.", nameof(states));
        }

        LanguageName = languageName;
        States = states;
    }

    /// <summary>Per-token callback invoked by <see cref="Tokenise(string, TokenSink)"/>.</summary>
    /// <param name="offset">UTF-16 offset of the token in the source string.</param>
    /// <param name="length">UTF-16 length of the token.</param>
    /// <param name="tokenClass">Classification.</param>
    public delegate void TokenSink(int offset, int length, TokenClass tokenClass);

    /// <summary>Per-token callback that carries an additional state value, eliminating closure allocations in the caller.</summary>
    /// <typeparam name="TState">Caller-supplied state shape.</typeparam>
    /// <param name="state">Caller-supplied state.</param>
    /// <param name="offset">UTF-16 offset of the token in the source string.</param>
    /// <param name="length">UTF-16 length of the token.</param>
    /// <param name="tokenClass">Classification.</param>
    public delegate void TokenSink<in TState>(TState state, int offset, int length, TokenClass tokenClass);

    /// <summary>Gets the initial state name expected in <see cref="States"/>.</summary>
    public static string RootState => "root";

    /// <summary>Gets the language name this lexer handles.</summary>
    public string LanguageName { get; }

    /// <summary>Gets the state map.</summary>
    public FrozenDictionary<string, LexerRule[]> States { get; }

    /// <summary>Walks <paramref name="source"/> once, calling <paramref name="onToken"/> for each emitted token.</summary>
    /// <param name="source">UTF-16 source string (regex APIs require strings).</param>
    /// <param name="onToken">Callback invoked with offset, length, and classification.</param>
    public void Tokenise(string source, TokenSink onToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onToken);
        Tokenise(source, onToken, static (sink, offset, length, cls) => sink(offset, length, cls));
    }

    /// <summary>Walks <paramref name="source"/> once, calling <paramref name="onToken"/> with the caller-supplied <paramref name="state"/> for each emitted token.</summary>
    /// <typeparam name="TState">Caller-supplied state shape.</typeparam>
    /// <param name="source">UTF-16 source string.</param>
    /// <param name="state">State threaded through every callback invocation.</param>
    /// <param name="onToken">Callback invoked per token.</param>
    public void Tokenise<TState>(string source, TState state, TokenSink<TState> onToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onToken);
        if (source.Length is 0)
        {
            return;
        }

        var stack = RentStack();
        try
        {
            var pos = 0;
            while (pos < source.Length)
            {
                pos = StepOnce(source, pos, stack, state, onToken);
            }
        }
        finally
        {
            ReturnStack(stack);
        }
    }

    /// <summary>Rents a state stack from the per-thread cache (or creates one), pre-seeded with the root state.</summary>
    /// <returns>An initialised stack.</returns>
    private static Stack<string> RentStack()
    {
        var stack = _stateStackCache ?? new Stack<string>(StateStackInitialCapacity);
        _stateStackCache = null;
        stack.Clear();
        stack.Push(RootState);
        return stack;
    }

    /// <summary>Returns <paramref name="stack"/> to the per-thread cache; outliers above <see cref="StateStackMaxCachedCapacity"/> are dropped.</summary>
    /// <param name="stack">The stack to park.</param>
    private static void ReturnStack(Stack<string> stack)
    {
        if (stack.Count > StateStackMaxCachedCapacity)
        {
            return;
        }

        _stateStackCache = stack;
    }

    /// <summary>Applies a rule's <c>NextState</c> directive to the state stack.</summary>
    /// <param name="nextState">Directive (null = no change, <see cref="LexerRule.StatePop"/> = pop, anything else = push).</param>
    /// <param name="stateStack">Stack to mutate.</param>
    private static void ApplyTransition(string? nextState, Stack<string> stateStack)
    {
        if (nextState is null)
        {
            return;
        }

        if (nextState == LexerRule.StatePop)
        {
            if (stateStack.Count > 1)
            {
                stateStack.Pop();
            }

            return;
        }

        stateStack.Push(nextState);
    }

    /// <summary>Advances one step: tries each rule for the current state, falls back to a single-character text token on no match.</summary>
    /// <typeparam name="TState">Caller-supplied state shape.</typeparam>
    /// <param name="source">Full source string.</param>
    /// <param name="pos">Cursor.</param>
    /// <param name="stateStack">Mutable state stack.</param>
    /// <param name="state">State threaded through the callback.</param>
    /// <param name="onToken">Token sink.</param>
    /// <returns>Next cursor position.</returns>
    private int StepOnce<TState>(string source, int pos, Stack<string> stateStack, TState state, TokenSink<TState> onToken)
    {
        if (!States.TryGetValue(stateStack.Peek(), out var rules))
        {
            // Unknown state — emit the rest as text and stop.
            onToken(state, pos, source.Length - pos, TokenClass.Text);
            return source.Length;
        }

        var slice = source.AsSpan(pos);
        var first = slice[0];
        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];

            // First-character dispatch: rules whose start-set doesn't include the cursor
            // character are skipped without invoking the regex. Rules without a hint
            // (FirstChars == null) keep the original always-try behaviour.
            if (rule.FirstChars is { } firstChars && !firstChars.Contains(first))
            {
                continue;
            }

            // EnumerateMatches yields ValueMatch (a struct) instead of allocating a Match object per
            // call. Patterns are anchored with \G, so the only valid match is at slice offset 0.
            var enumerator = rule.Pattern.EnumerateMatches(slice);
            if (!enumerator.MoveNext())
            {
                continue;
            }

            var current = enumerator.Current;
            if (current.Index != 0 || current.Length is 0)
            {
                continue;
            }

            onToken(state, pos, current.Length, rule.TokenClass);
            ApplyTransition(rule.NextState, stateStack);
            return pos + current.Length;
        }

        // No rule matched — emit the cursor character as plain text so the walker always makes forward progress.
        onToken(state, pos, 1, TokenClass.Text);
        return pos + 1;
    }
}
