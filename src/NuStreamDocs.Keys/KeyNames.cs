// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Keys;

/// <summary>
/// Static map of the common pymdownx.keys aliases to their
/// canonical class slug + display label.
/// </summary>
/// <remarks>
/// Built once at startup so per-page lookups don't pay any setup cost. Keyed on the lowercase
/// UTF-8 token bytes; the rewriter probes via
/// <see cref="System.Collections.Generic.Dictionary{TKey, TValue}.AlternateLookup{TAlternateKey}"/>
/// keyed on <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> so the per-token dispatch never
/// allocates.
/// </remarks>
internal static class KeyNames
{
    /// <summary>Lookup table keyed on the lower-case UTF-8 token bytes from the source markup.</summary>
    private static readonly Dictionary<byte[], KeyEntry> Map = BuildMap();

    /// <summary>Span-keyed alternate lookup over <see cref="Map"/>.</summary>
    private static readonly Dictionary<byte[], KeyEntry>.AlternateLookup<ReadOnlySpan<byte>> SpanLookup =
        Map.AsUtf8Lookup();

    /// <summary>Tries to resolve <paramref name="token"/> against the alias map.</summary>
    /// <param name="token">Lower-case UTF-8 token bytes from the source markup.</param>
    /// <param name="entry">Resolved entry on success.</param>
    /// <returns>True when the token is a known alias.</returns>
    public static bool TryGet(ReadOnlySpan<byte> token, out KeyEntry entry) =>
        SpanLookup.TryGetValue(token, out entry);

    /// <summary>Builds the alias map.</summary>
    /// <returns>Byte-keyed dictionary with <see cref="ByteArrayComparer.Instance"/>.</returns>
    private static Dictionary<byte[], KeyEntry> BuildMap() =>
        new(ByteArrayComparer.Instance)
        {
            // modifiers
            [[.. "ctrl"u8]] = new([.. "ctrl"u8], [.. "Ctrl"u8]),
            [[.. "control"u8]] = new([.. "ctrl"u8], [.. "Ctrl"u8]),
            [[.. "alt"u8]] = new([.. "alt"u8], [.. "Alt"u8]),
            [[.. "option"u8]] = new([.. "alt"u8], [.. "Alt"u8]),
            [[.. "shift"u8]] = new([.. "shift"u8], [.. "Shift"u8]),
            [[.. "meta"u8]] = new([.. "meta"u8], [.. "Meta"u8]),
            [[.. "cmd"u8]] = new([.. "cmd"u8], [.. "Cmd"u8]),
            [[.. "command"u8]] = new([.. "cmd"u8], [.. "Cmd"u8]),
            [[.. "win"u8]] = new([.. "windows"u8], [.. "Win"u8]),
            [[.. "windows"u8]] = new([.. "windows"u8], [.. "Win"u8]),
            [[.. "super"u8]] = new([.. "super"u8], [.. "Super"u8]),

            // navigation / editing
            [[.. "enter"u8]] = new([.. "enter"u8], [.. "Enter"u8]),
            [[.. "return"u8]] = new([.. "enter"u8], [.. "Enter"u8]),
            [[.. "tab"u8]] = new([.. "tab"u8], [.. "Tab"u8]),
            [[.. "space"u8]] = new([.. "space"u8], [.. "Space"u8]),
            [[.. "spacebar"u8]] = new([.. "space"u8], [.. "Space"u8]),
            [[.. "backspace"u8]] = new([.. "backspace"u8], [.. "Backspace"u8]),
            [[.. "delete"u8]] = new([.. "delete"u8], [.. "Delete"u8]),
            [[.. "del"u8]] = new([.. "delete"u8], [.. "Delete"u8]),
            [[.. "escape"u8]] = new([.. "escape"u8], [.. "Esc"u8]),
            [[.. "esc"u8]] = new([.. "escape"u8], [.. "Esc"u8]),
            [[.. "insert"u8]] = new([.. "insert"u8], [.. "Insert"u8]),
            [[.. "ins"u8]] = new([.. "insert"u8], [.. "Insert"u8]),
            [[.. "home"u8]] = new([.. "home"u8], [.. "Home"u8]),
            [[.. "end"u8]] = new([.. "end"u8], [.. "End"u8]),
            [[.. "pageup"u8]] = new([.. "page-up"u8], [.. "PgUp"u8]),
            [[.. "pgup"u8]] = new([.. "page-up"u8], [.. "PgUp"u8]),
            [[.. "pagedown"u8]] = new([.. "page-down"u8], [.. "PgDn"u8]),
            [[.. "pgdn"u8]] = new([.. "page-down"u8], [.. "PgDn"u8]),

            // arrows
            [[.. "up"u8]] = new([.. "arrow-up"u8], [.. "↑"u8]),
            [[.. "down"u8]] = new([.. "arrow-down"u8], [.. "↓"u8]),
            [[.. "left"u8]] = new([.. "arrow-left"u8], [.. "←"u8]),
            [[.. "right"u8]] = new([.. "arrow-right"u8], [.. "→"u8]),

            // common punctuation
            [[.. "plus"u8]] = new([.. "plus"u8], [.. "+"u8]),
            [[.. "minus"u8]] = new([.. "minus"u8], [.. "-"u8]),
            [[.. "equal"u8]] = new([.. "equal"u8], [.. "="u8]),
            [[.. "comma"u8]] = new([.. "comma"u8], [.. ","u8]),
            [[.. "period"u8]] = new([.. "period"u8], [.. "."u8]),
            [[.. "semicolon"u8]] = new([.. "semicolon"u8], [.. ";"u8]),
            [[.. "slash"u8]] = new([.. "slash"u8], [.. "/"u8]),
            [[.. "backslash"u8]] = new([.. "backslash"u8], [.. "\\"u8]),

            // function row
            [[.. "f1"u8]] = new([.. "f1"u8], [.. "F1"u8]),
            [[.. "f2"u8]] = new([.. "f2"u8], [.. "F2"u8]),
            [[.. "f3"u8]] = new([.. "f3"u8], [.. "F3"u8]),
            [[.. "f4"u8]] = new([.. "f4"u8], [.. "F4"u8]),
            [[.. "f5"u8]] = new([.. "f5"u8], [.. "F5"u8]),
            [[.. "f6"u8]] = new([.. "f6"u8], [.. "F6"u8]),
            [[.. "f7"u8]] = new([.. "f7"u8], [.. "F7"u8]),
            [[.. "f8"u8]] = new([.. "f8"u8], [.. "F8"u8]),
            [[.. "f9"u8]] = new([.. "f9"u8], [.. "F9"u8]),
            [[.. "f10"u8]] = new([.. "f10"u8], [.. "F10"u8]),
            [[.. "f11"u8]] = new([.. "f11"u8], [.. "F11"u8]),
            [[.. "f12"u8]] = new([.. "f12"u8], [.. "F12"u8])
        };
}
