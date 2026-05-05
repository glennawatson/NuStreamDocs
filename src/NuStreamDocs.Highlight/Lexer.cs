// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Highlight;

/// <summary>
/// A compiled state-machine lexer that operates on UTF-8 byte spans.
/// </summary>
/// <remarks>
/// A lexer is an array of <see cref="LexerRule"/> arrays indexed by integer state id;
/// <see cref="Tokenize(ReadOnlySpan{byte}, TokenSink)"/> walks the input once,
/// advancing the cursor past the first rule that matches at each position.
/// On no match the cursor advances by one byte with the <see cref="TokenClass.Text"/>
/// classification, which guarantees forward progress and never throws from tokenization.
/// <para>
/// Built-in lexers ship <see cref="LexerRuleMatcher"/> methods backed by
/// <see cref="TokenMatchers"/>, so the lexer instance is reused across every fenced block
/// on every page without per-call allocation. Each matcher is a static method that returns
/// the matched length in bytes.
/// </para>
/// <para>
/// The per-call state stack uses caller stack memory for the common shallow case and rents
/// from <see cref="ArrayPool{T}"/> only when the lexer state stack grows beyond the initial
/// inline capacity. The hot token-emission path can use a struct sink to avoid delegate and
/// closure allocation risk.
/// </para>
/// </remarks>
public sealed class Lexer
{
    /// <summary>The integer id of every lexer's root state.</summary>
    internal const int RootStateId = 0;

    /// <summary>Initial inline capacity for the state stack; most languages stay shallow.</summary>
    private const int StateStackInitialCapacity = 8;

    /// <summary>Initializes a new instance of the <see cref="Lexer"/> class.</summary>
    /// <param name="states">Rule list per state, indexed by state id; <c>states[<see cref="RootStateId"/>]</c> is the starting state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="states"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="states"/> does not contain a root state.</exception>
    public Lexer(LexerRule[][] states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Length is 0)
        {
            throw new ArgumentException("Lexer state table must contain at least the root state.", nameof(states));
        }

        States = states;
    }

    /// <summary>Per-token callback invoked by <see cref="Tokenize(ReadOnlySpan{byte}, TokenSink)"/>.</summary>
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

    /// <summary>
    /// Allocation-free token sink contract for hot callers that want to avoid delegate and closure allocation risk.
    /// </summary>
    public interface ITokenSink
    {
        /// <summary>Receives a token emitted by the lexer.</summary>
        /// <param name="offset">UTF-8 byte offset of the token in the source span.</param>
        /// <param name="length">UTF-8 byte length of the token.</param>
        /// <param name="tokenClass">Classification.</param>
        void OnToken(int offset, int length, TokenClass tokenClass);
    }

    /// <summary>Gets the per-state rule table.</summary>
    public LexerRule[][] States { get; }

    /// <summary>Walks <paramref name="source"/> once, calling <paramref name="onToken"/> for each emitted token.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="onToken">Callback invoked with offset, length, and classification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onToken"/> is <see langword="null"/>.</exception>
    public void Tokenize(ReadOnlySpan<byte> source, TokenSink onToken)
    {
        ArgumentNullException.ThrowIfNull(onToken);

        DelegateTokenSink sink = new(onToken);
        Tokenize(source, ref sink);
    }

    /// <summary>Walks <paramref name="source"/> once, calling <paramref name="onToken"/> with the caller-supplied <paramref name="state"/> for each emitted token.</summary>
    /// <typeparam name="TState">Caller-supplied state shape.</typeparam>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="state">State threaded through every callback invocation.</param>
    /// <param name="onToken">Callback invoked per token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onToken"/> is <see langword="null"/>.</exception>
    public void Tokenize<TState>(ReadOnlySpan<byte> source, TState state, TokenSink<TState> onToken)
    {
        ArgumentNullException.ThrowIfNull(onToken);

        DelegateTokenSink<TState> sink = new(state, onToken);
        Tokenize(source, ref sink);
    }

    /// <summary>
    /// Walks <paramref name="source"/> once, calling <paramref name="sink"/> for each emitted token.
    /// </summary>
    /// <typeparam name="TSink">Caller-supplied sink shape.</typeparam>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="sink">Sink invoked per token. Passed by reference to avoid copying stateful structs.</param>
    [SuppressMessage(
        "Design",
        "CA1045:Do not pass types by reference",
        Justification = "Needed for allocation-free token emission with mutable struct sinks.")]
    public void Tokenize<TSink>(ReadOnlySpan<byte> source, ref TSink sink)
        where TSink : struct, ITokenSink
    {
        if (source.Length is 0)
        {
            return;
        }

        StateStack stateStack = new(stackalloc int[StateStackInitialCapacity]);

        try
        {
            stateStack.Push(RootStateId);

            // Cache the current state's rule array across token steps and only refetch
            // when the top of the state stack changes. State ids are simple ints, so
            // the equality check is a single instruction. This cuts the indirect array
            // lookup out of the per-token hot path; for single-state lexers, the table
            // is consulted once for the whole file.
            var pos = 0;
            var cachedStateId = -1;
            LexerRule[]? cachedRules = null;

            while (pos < source.Length)
            {
                var currentStateId = stateStack.Peek();
                if (currentStateId != cachedStateId)
                {
                    cachedStateId = currentStateId;
                    if ((uint)currentStateId >= (uint)States.Length)
                    {
                        // Unknown state id — emit the rest as text and stop.
                        sink.OnToken(pos, source.Length - pos, TokenClass.Text);
                        return;
                    }

                    cachedRules = States[currentStateId];
                }

                pos = StepOnce(source, pos, cachedRules!, ref stateStack, ref sink);
            }
        }
        finally
        {
            stateStack.Dispose();
        }
    }

    /// <summary>Applies a rule's <c>NextState</c> directive to the state stack.</summary>
    /// <param name="nextState">Directive (<see cref="LexerRule.NoStateChange"/> = no change, <see cref="LexerRule.PopState"/> = pop, anything else = push).</param>
    /// <param name="stateStack">Stack to mutate.</param>
    private static void ApplyTransition(int nextState, ref StateStack stateStack)
    {
        switch (nextState)
        {
            case LexerRule.NoStateChange:
                return;
            case LexerRule.PopState:
                {
                    stateStack.PopIfNotRoot();
                    return;
                }

            default:
                {
                    stateStack.Push(nextState);
                    break;
                }
        }
    }

    /// <summary>Advances one step: tries each rule for the current state, falls back to a single-byte text token on no match.</summary>
    /// <typeparam name="TSink">Caller-supplied sink shape.</typeparam>
    /// <param name="source">Full source span.</param>
    /// <param name="pos">Cursor.</param>
    /// <param name="rules">Rule list for the current state — looked up by the caller and cached across steps.</param>
    /// <param name="stateStack">Mutable state stack.</param>
    /// <param name="sink">Token sink.</param>
    /// <returns>Next cursor position.</returns>
    private static int StepOnce<TSink>(
        ReadOnlySpan<byte> source,
        int pos,
        LexerRule[] rules,
        ref StateStack stateStack,
        ref TSink sink)
        where TSink : struct, ITokenSink
    {
        var slice = source[pos..];
        var first = slice[0];

        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];

            // First-byte dispatch: rules whose start-set does not include the cursor
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

            sink.OnToken(pos, matched, rule.TokenClass);
            ApplyTransition(rule.NextState, ref stateStack);
            return pos + matched;
        }

        // No rule matched — emit the cursor byte as plain text so the walker always makes forward progress.
        sink.OnToken(pos, 1, TokenClass.Text);
        return pos + 1;
    }

    /// <summary>Delegate-backed adapter for the <see cref="TokenSink"/> overload.</summary>
    private readonly record struct DelegateTokenSink : ITokenSink
    {
        /// <summary>The underlying sink delegate.</summary>
        private readonly TokenSink _sink;

        /// <summary>Initializes a new instance of the <see cref="DelegateTokenSink"/> struct.</summary>
        /// <param name="sink">The sink delegate.</param>
        public DelegateTokenSink(TokenSink sink) => _sink = sink;

        /// <inheritdoc />
        public void OnToken(int offset, int length, TokenClass tokenClass) => _sink(offset, length, tokenClass);
    }

    /// <summary>Delegate-backed adapter for the <see cref="TokenSink{TState}"/> overload.</summary>
    /// <typeparam name="TState">The caller-supplied state type.</typeparam>
    private readonly record struct DelegateTokenSink<TState> : ITokenSink
    {
        /// <summary>The caller-supplied state.</summary>
        private readonly TState _state;

        /// <summary>The underlying sink delegate.</summary>
        private readonly TokenSink<TState> _sink;

        /// <summary>Initializes a new instance of the <see cref="DelegateTokenSink{TState}"/> struct.</summary>
        /// <param name="state">The state threaded through the callback.</param>
        /// <param name="sink">The sink delegate.</param>
        public DelegateTokenSink(TState state, TokenSink<TState> sink)
        {
            _state = state;
            _sink = sink;
        }

        /// <inheritdoc />
        public void OnToken(int offset, int length, TokenClass tokenClass) => _sink(_state, offset, length, tokenClass);
    }

    /// <summary>Lexer state stack backed by stack memory or <see cref="ArrayPool{T}"/>.</summary>
    private ref struct StateStack
    {
        /// <summary>The active stack storage.</summary>
        private Span<int> _items;

        /// <summary>The rented array, if the stack grew beyond the caller-provided initial buffer.</summary>
        private int[]? _rented;

        /// <summary>The number of active items in <see cref="_items"/>.</summary>
        private int _count;

        /// <summary>Initializes a new instance of the <see cref="StateStack"/> struct.</summary>
        /// <param name="initialBuffer">The initial stack buffer, normally backed by <see langword="stackalloc"/>.</param>
        public StateStack(Span<int> initialBuffer)
        {
            _items = initialBuffer;
            _rented = null;
            _count = 0;
        }

        /// <summary>Peeks the current top state id.</summary>
        /// <returns>The state id at the top of the stack.</returns>
        public readonly int Peek() => _items[_count - 1];

        /// <summary>Pushes a state id onto the stack.</summary>
        /// <param name="value">The state id to push.</param>
        public void Push(int value)
        {
            if ((uint)_count >= (uint)_items.Length)
            {
                Grow();
            }

            _items[_count++] = value;
        }

        /// <summary>Pops the top state id if doing so would not remove the root state.</summary>
        public void PopIfNotRoot()
        {
            if (_count <= 1)
            {
                return;
            }

            _count--;
        }

        /// <summary>Returns any rented storage to the shared array pool.</summary>
        public void Dispose()
        {
            var rented = _rented;
            if (rented is null)
            {
                return;
            }

            _rented = null;
            ArrayPool<int>.Shared.Return(rented);
        }

        /// <summary>Grows the stack storage, preserving all active state ids.</summary>
        /// <remarks>
        /// The method uses a commit flag so the newly rented array is returned if copying fails,
        /// while the previous rented array is returned only after the new storage has become active.
        /// This avoids leaking a newly rented array and avoids returning the active array prematurely.
        /// </remarks>
        private void Grow()
        {
            var newCapacity = _items.Length == 0
                ? StateStackInitialCapacity
                : _items.Length * 2;

            var previous = _rented;
            var rented = ArrayPool<int>.Shared.Rent(newCapacity);
            var committed = false;

            try
            {
                _items[.._count].CopyTo(rented);
                _items = rented;
                _rented = rented;
                committed = true;
            }
            finally
            {
                if (committed)
                {
                    if (previous is not null)
                    {
                        ArrayPool<int>.Shared.Return(previous);
                    }
                }
                else
                {
                    ArrayPool<int>.Shared.Return(rented);
                }
            }
        }
    }
}
