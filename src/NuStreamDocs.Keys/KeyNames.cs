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
            ["ctrl"u8.ToArray()] = new("ctrl"u8.ToArray(), "Ctrl"u8.ToArray()),
            ["control"u8.ToArray()] = new("ctrl"u8.ToArray(), "Ctrl"u8.ToArray()),
            ["alt"u8.ToArray()] = new("alt"u8.ToArray(), "Alt"u8.ToArray()),
            ["option"u8.ToArray()] = new("alt"u8.ToArray(), "Alt"u8.ToArray()),
            ["shift"u8.ToArray()] = new("shift"u8.ToArray(), "Shift"u8.ToArray()),
            ["meta"u8.ToArray()] = new("meta"u8.ToArray(), "Meta"u8.ToArray()),
            ["cmd"u8.ToArray()] = new("cmd"u8.ToArray(), "Cmd"u8.ToArray()),
            ["command"u8.ToArray()] = new("cmd"u8.ToArray(), "Cmd"u8.ToArray()),
            ["win"u8.ToArray()] = new("windows"u8.ToArray(), "Win"u8.ToArray()),
            ["windows"u8.ToArray()] = new("windows"u8.ToArray(), "Win"u8.ToArray()),
            ["super"u8.ToArray()] = new("super"u8.ToArray(), "Super"u8.ToArray()),

            // navigation / editing
            ["enter"u8.ToArray()] = new("enter"u8.ToArray(), "Enter"u8.ToArray()),
            ["return"u8.ToArray()] = new("enter"u8.ToArray(), "Enter"u8.ToArray()),
            ["tab"u8.ToArray()] = new("tab"u8.ToArray(), "Tab"u8.ToArray()),
            ["space"u8.ToArray()] = new("space"u8.ToArray(), "Space"u8.ToArray()),
            ["spacebar"u8.ToArray()] = new("space"u8.ToArray(), "Space"u8.ToArray()),
            ["backspace"u8.ToArray()] = new("backspace"u8.ToArray(), "Backspace"u8.ToArray()),
            ["delete"u8.ToArray()] = new("delete"u8.ToArray(), "Delete"u8.ToArray()),
            ["del"u8.ToArray()] = new("delete"u8.ToArray(), "Delete"u8.ToArray()),
            ["escape"u8.ToArray()] = new("escape"u8.ToArray(), "Esc"u8.ToArray()),
            ["esc"u8.ToArray()] = new("escape"u8.ToArray(), "Esc"u8.ToArray()),
            ["insert"u8.ToArray()] = new("insert"u8.ToArray(), "Insert"u8.ToArray()),
            ["ins"u8.ToArray()] = new("insert"u8.ToArray(), "Insert"u8.ToArray()),
            ["home"u8.ToArray()] = new("home"u8.ToArray(), "Home"u8.ToArray()),
            ["end"u8.ToArray()] = new("end"u8.ToArray(), "End"u8.ToArray()),
            ["pageup"u8.ToArray()] = new("page-up"u8.ToArray(), "PgUp"u8.ToArray()),
            ["pgup"u8.ToArray()] = new("page-up"u8.ToArray(), "PgUp"u8.ToArray()),
            ["pagedown"u8.ToArray()] = new("page-down"u8.ToArray(), "PgDn"u8.ToArray()),
            ["pgdn"u8.ToArray()] = new("page-down"u8.ToArray(), "PgDn"u8.ToArray()),

            // arrows
            ["up"u8.ToArray()] = new("arrow-up"u8.ToArray(), "↑"u8.ToArray()),
            ["down"u8.ToArray()] = new("arrow-down"u8.ToArray(), "↓"u8.ToArray()),
            ["left"u8.ToArray()] = new("arrow-left"u8.ToArray(), "←"u8.ToArray()),
            ["right"u8.ToArray()] = new("arrow-right"u8.ToArray(), "→"u8.ToArray()),

            // common punctuation
            ["plus"u8.ToArray()] = new("plus"u8.ToArray(), "+"u8.ToArray()),
            ["minus"u8.ToArray()] = new("minus"u8.ToArray(), "-"u8.ToArray()),
            ["equal"u8.ToArray()] = new("equal"u8.ToArray(), "="u8.ToArray()),
            ["comma"u8.ToArray()] = new("comma"u8.ToArray(), ","u8.ToArray()),
            ["period"u8.ToArray()] = new("period"u8.ToArray(), "."u8.ToArray()),
            ["semicolon"u8.ToArray()] = new("semicolon"u8.ToArray(), ";"u8.ToArray()),
            ["slash"u8.ToArray()] = new("slash"u8.ToArray(), "/"u8.ToArray()),
            ["backslash"u8.ToArray()] = new("backslash"u8.ToArray(), "\\"u8.ToArray()),

            // function row
            ["f1"u8.ToArray()] = new("f1"u8.ToArray(), "F1"u8.ToArray()),
            ["f2"u8.ToArray()] = new("f2"u8.ToArray(), "F2"u8.ToArray()),
            ["f3"u8.ToArray()] = new("f3"u8.ToArray(), "F3"u8.ToArray()),
            ["f4"u8.ToArray()] = new("f4"u8.ToArray(), "F4"u8.ToArray()),
            ["f5"u8.ToArray()] = new("f5"u8.ToArray(), "F5"u8.ToArray()),
            ["f6"u8.ToArray()] = new("f6"u8.ToArray(), "F6"u8.ToArray()),
            ["f7"u8.ToArray()] = new("f7"u8.ToArray(), "F7"u8.ToArray()),
            ["f8"u8.ToArray()] = new("f8"u8.ToArray(), "F8"u8.ToArray()),
            ["f9"u8.ToArray()] = new("f9"u8.ToArray(), "F9"u8.ToArray()),
            ["f10"u8.ToArray()] = new("f10"u8.ToArray(), "F10"u8.ToArray()),
            ["f11"u8.ToArray()] = new("f11"u8.ToArray(), "F11"u8.ToArray()),
            ["f12"u8.ToArray()] = new("f12"u8.ToArray(), "F12"u8.ToArray()),
        };
}
