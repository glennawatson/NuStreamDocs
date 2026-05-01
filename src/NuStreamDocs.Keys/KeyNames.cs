// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace NuStreamDocs.Keys;

/// <summary>
/// Static map of the common pymdownx.keys aliases to their
/// canonical class slug + display label. Built once at startup as
/// a <see cref="FrozenDictionary{TKey, TValue}"/> so per-page
/// lookups stay branch-free in the rewriter hot path.
/// </summary>
internal static class KeyNames
{
    /// <summary>Lookup table keyed on the lower-case token from the source markup.</summary>
    private static readonly FrozenDictionary<string, KeyEntry> Map = BuildMap();

    /// <summary>Tries to resolve <paramref name="token"/> against the alias map.</summary>
    /// <param name="token">Lower-case token from the source markup.</param>
    /// <param name="entry">Resolved entry on success.</param>
    /// <returns>True when the token is a known alias.</returns>
    public static bool TryGet(string token, out KeyEntry entry) => Map.TryGetValue(token, out entry);

    /// <summary>Builds the alias map.</summary>
    /// <returns>Frozen dictionary keyed on the alias.</returns>
    private static FrozenDictionary<string, KeyEntry> BuildMap()
    {
        var seed = new Dictionary<string, KeyEntry>(StringComparer.Ordinal)
        {
            // modifiers
            ["ctrl"] = new("ctrl", "Ctrl"),
            ["control"] = new("ctrl", "Ctrl"),
            ["alt"] = new("alt", "Alt"),
            ["option"] = new("alt", "Alt"),
            ["shift"] = new("shift", "Shift"),
            ["meta"] = new("meta", "Meta"),
            ["cmd"] = new("cmd", "Cmd"),
            ["command"] = new("cmd", "Cmd"),
            ["win"] = new("windows", "Win"),
            ["windows"] = new("windows", "Win"),
            ["super"] = new("super", "Super"),

            // navigation / editing
            ["enter"] = new("enter", "Enter"),
            ["return"] = new("enter", "Enter"),
            ["tab"] = new("tab", "Tab"),
            ["space"] = new("space", "Space"),
            ["spacebar"] = new("space", "Space"),
            ["backspace"] = new("backspace", "Backspace"),
            ["delete"] = new("delete", "Delete"),
            ["del"] = new("delete", "Delete"),
            ["escape"] = new("escape", "Esc"),
            ["esc"] = new("escape", "Esc"),
            ["insert"] = new("insert", "Insert"),
            ["ins"] = new("insert", "Insert"),
            ["home"] = new("home", "Home"),
            ["end"] = new("end", "End"),
            ["pageup"] = new("page-up", "PgUp"),
            ["pgup"] = new("page-up", "PgUp"),
            ["pagedown"] = new("page-down", "PgDn"),
            ["pgdn"] = new("page-down", "PgDn"),

            // arrows
            ["up"] = new("arrow-up", "↑"),
            ["down"] = new("arrow-down", "↓"),
            ["left"] = new("arrow-left", "←"),
            ["right"] = new("arrow-right", "→"),

            // common punctuation
            ["plus"] = new("plus", "+"),
            ["minus"] = new("minus", "-"),
            ["equal"] = new("equal", "="),
            ["comma"] = new("comma", ","),
            ["period"] = new("period", "."),
            ["semicolon"] = new("semicolon", ";"),
            ["slash"] = new("slash", "/"),
            ["backslash"] = new("backslash", "\\"),

            // function row
            ["f1"] = new("f1", "F1"),
            ["f2"] = new("f2", "F2"),
            ["f3"] = new("f3", "F3"),
            ["f4"] = new("f4", "F4"),
            ["f5"] = new("f5", "F5"),
            ["f6"] = new("f6", "F6"),
            ["f7"] = new("f7", "F7"),
            ["f8"] = new("f8", "F8"),
            ["f9"] = new("f9", "F9"),
            ["f10"] = new("f10", "F10"),
            ["f11"] = new("f11", "F11"),
            ["f12"] = new("f12", "F12"),
        };

        return seed.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
