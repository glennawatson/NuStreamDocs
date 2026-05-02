// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>
/// A compiled Pygments-shape state-machine lexer that operates on UTF-8 byte spans.
/// </summary>
/// <remarks>
/// A lexer is an array of <see cref="LexerRule"/> arrays indexed by
/// integer state id; <see cref="Tokenise(ReadOnlySpan{byte}, TokenSink)"/>
/// walks the input once, advancing the cursor past the longest rule that
/// matches at each position. On no match the cursor advances by one byte
/// with the <see cref="TokenClass.Text"/> classification — guarantees
/// forward progress and never throws.
/// <para>
/// Built-in lexers ship <see cref="LexerRuleMatcher"/> methods backed
/// by <see cref="TokenMatchers"/>, so the lexer instance is reused
/// across every fenced block on every page without per-call allocation.
/// Each matcher is a static method that returns the matched length in
/// bytes. The per-call state stack is rented from a thread-static cache
/// so a typical call allocates nothing.
/// </para>
/// </remarks>
public sealed class Lexer
{
    /// <summary>The integer id of every lexer's root state.</summary>
    internal const int RootStateId = 0;

    /// <summary>Initial capacity for the rented state stack; most languages stay shallow.</summary>
    private const int StateStackInitialCapacity = 8;

    /// <summary>Cap above which a parked stack is dropped instead of cached, so a pathological corpus doesn't pin a worker's stack forever.</summary>
    private const int StateStackMaxCachedCapacity = 64;

    /// <summary>Per-thread parked state stack reused across <see cref="Tokenise(ReadOnlySpan{byte}, TokenSink)"/> calls on the same worker.</summary>
    [ThreadStatic]
    private static Stack<int>? _stateStackCache;

    /// <summary>Initializes a new instance of the <see cref="Lexer"/> class.</summary>
    /// <param name="languageName">Display / matching name (e.g. <c>csharp</c>).</param>
    /// <param name="states">Rule list per state, indexed by state id; <c>states[<see cref="RootStateId"/>]</c> is the starting state.</param>
    public Lexer(string languageName, LexerRule[][] states)
    {
        ArgumentException.ThrowIfNullOrEmpty(languageName);
        ArgumentNullException.ThrowIfNull(states);
        if (states.Length is 0)
        {
            throw new ArgumentException($"Lexer state table for '{languageName}' must contain at least the root state.", nameof(states));
        }

        LanguageName = languageName;
        States = states;
    }

    /// <summary>Per-token callback invoked by <see cref="Tokenise(ReadOnlySpan{byte}, TokenSink)"/>.</summary>
    /// <param name="offset">UTF-8 byte offset of the token in the source span.</param>
    /// <param name="length">UTF-8 byte length of the token.</param>
    /// <param name="tokenClass">Classification.</param>
    public delegate void TokenSink(int offset, int length, TokenClass tokenClass);

    /// <summary>Per-token callback that carries an additional state value, eliminating closure allocations in the caller.</summary>
    /// <typeparam name="TState">Caller-supplied state shape.</typeparam>
    /// <param name="state">Caller-supplied state.</param>
    /// <param name="offset">UTF-8 byte offset of the token in the source span.</param>
    /// <param name="length">UTF-8 byte length of the token.</param>
    /// <param name="tokenClass">Classification.</param>
    public delegate void TokenSink<in TState>(TState state, int offset, int length, TokenClass tokenClass);

    /// <summary>Gets the language name this lexer handles.</summary>
    public string LanguageName { get; }

    /// <summary>Gets the per-state rule table.</summary>
    public LexerRule[][] States { get; }

    /// <summary>Walks <paramref name="source"/> once, calling <paramref name="onToken"/> for each emitted token.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="onToken">Callback invoked with offset, length, and classification.</param>
    public void Tokenise(ReadOnlySpan<byte> source, TokenSink onToken)
    {
        ArgumentNullException.ThrowIfNull(onToken);
        Tokenise(source, onToken, static (sink, offset, length, cls) => sink(offset, length, cls));
    }

    /// <summary>Walks <paramref name="source"/> once, calling <paramref name="onToken"/> with the caller-supplied <paramref name="state"/> for each emitted token.</summary>
    /// <typeparam name="TState">Caller-supplied state shape.</typeparam>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="state">State threaded through every callback invocation.</param>
    /// <param name="onToken">Callback invoked per token.</param>
    public void Tokenise<TState>(ReadOnlySpan<byte> source, TState state, TokenSink<TState> onToken)
    {
        ArgumentNullException.ThrowIfNull(onToken);
        if (source.Length is 0)
        {
            return;
        }

        var stack = RentStack();
        try
        {
            // Cache the current state's rule array across token steps and only refetch
            // when the top of the state stack changes. State ids are simple ints, so
            // the equality check is a single instruction. Cuts the indirect array
            // lookup out of the per-token hot path — for single-state lexers (most),
            // the table is consulted once for the whole file.
            var pos = 0;
            var cachedStateId = -1;
            LexerRule[]? cachedRules = null;
            while (pos < source.Length)
            {
                var currentStateId = stack.Peek();
                if (currentStateId != cachedStateId)
                {
                    cachedStateId = currentStateId;
                    if ((uint)currentStateId >= (uint)States.Length)
                    {
                        // Unknown state id — emit the rest as text and stop.
                        onToken(state, pos, source.Length - pos, TokenClass.Text);
                        return;
                    }

                    cachedRules = States[currentStateId];
                }

                pos = StepOnce(source, pos, cachedRules!, stack, state, onToken);
            }
        }
        finally
        {
            ReturnStack(stack);
        }
    }

    /// <summary>Rents a state stack from the per-thread cache (or creates one), pre-seeded with the root state id.</summary>
    /// <returns>An initialised stack.</returns>
    private static Stack<int> RentStack()
    {
        var stack = _stateStackCache ?? new Stack<int>(StateStackInitialCapacity);
        _stateStackCache = null;
        stack.Clear();
        stack.Push(RootStateId);
        return stack;
    }

    /// <summary>Returns <paramref name="stack"/> to the per-thread cache; outliers above <see cref="StateStackMaxCachedCapacity"/> are dropped.</summary>
    /// <param name="stack">The stack to park.</param>
    private static void ReturnStack(Stack<int> stack)
    {
        if (stack.Count > StateStackMaxCachedCapacity)
        {
            return;
        }

        _stateStackCache = stack;
    }

    /// <summary>Applies a rule's <c>NextState</c> directive to the state stack.</summary>
    /// <param name="nextState">Directive (<see cref="LexerRule.NoStateChange"/> = no change, <see cref="LexerRule.PopState"/> = pop, anything else = push).</param>
    /// <param name="stateStack">Stack to mutate.</param>
    private static void ApplyTransition(int nextState, Stack<int> stateStack)
    {
        if (nextState == LexerRule.NoStateChange)
        {
            return;
        }

        if (nextState == LexerRule.PopState)
        {
            if (stateStack.Count > 1)
            {
                stateStack.Pop();
            }

            return;
        }

        stateStack.Push(nextState);
    }

    /// <summary>Advances one step: tries each rule for the current state, falls back to a single-byte text token on no match.</summary>
    /// <typeparam name="TState">Caller-supplied state shape.</typeparam>
    /// <param name="source">Full source span.</param>
    /// <param name="pos">Cursor.</param>
    /// <param name="rules">Rule list for the current state — looked up by the caller and cached across steps.</param>
    /// <param name="stateStack">Mutable state stack.</param>
    /// <param name="state">State threaded through the callback.</param>
    /// <param name="onToken">Token sink.</param>
    /// <returns>Next cursor position.</returns>
    private static int StepOnce<TState>(ReadOnlySpan<byte> source, int pos, LexerRule[] rules, Stack<int> stateStack, TState state, TokenSink<TState> onToken)
    {
        var slice = source[pos..];
        var first = slice[0];
        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];

            // First-byte dispatch: rules whose start-set doesn't include the cursor
            // byte are skipped without invoking the matcher. Rules without a hint
            // (FirstBytes == null) always run the matcher.
            if (rule.FirstBytes is { } firstBytes && !firstBytes.Contains(first))
            {
                continue;
            }

            // Line-anchored rules only fire at start-of-input or after a line terminator.
            if (rule.RequiresLineStart && pos > 0 && source[pos - 1] is not ((byte)'\n' or (byte)'\r'))
            {
                continue;
            }

            var matched = rule.Match(slice);
            if (matched is 0)
            {
                continue;
            }

            onToken(state, pos, matched, rule.TokenClass);
            ApplyTransition(rule.NextState, stateStack);
            return pos + matched;
        }

        // No rule matched — emit the cursor byte as plain text so the walker always makes forward progress.
        onToken(state, pos, 1, TokenClass.Text);
        return pos + 1;
    }
}
