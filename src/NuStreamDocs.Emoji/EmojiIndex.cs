// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace NuStreamDocs.Emoji;

/// <summary>
/// Built-in shortcode → Unicode glyph map. Built once at startup
/// as a <see cref="FrozenDictionary{TKey, TValue}"/> so the
/// rewriter's per-page lookups stay branch-free in the hot path.
/// </summary>
/// <remarks>
/// The seed list focuses on the ~80 shortcodes that account for
/// the bulk of real-world Markdown usage (GitHub issue threads,
/// release notes, blog posts). Project consumers that need the
/// full Twemoji set can register additional plugins after this
/// one in the preprocessor chain — unmatched shortcodes pass
/// through verbatim.
/// </remarks>
internal static class EmojiIndex
{
    /// <summary>Lookup table keyed on the shortcode without colons.</summary>
    private static readonly FrozenDictionary<string, string> Map = BuildMap();

    /// <summary>Tries to resolve <paramref name="shortcode"/> against the index.</summary>
    /// <param name="shortcode">Shortcode without the surrounding colons.</param>
    /// <param name="glyph">Resolved Unicode glyph on success.</param>
    /// <returns>True when the shortcode is a known alias.</returns>
    public static bool TryGet(string shortcode, out string glyph) => Map.TryGetValue(shortcode, out glyph!);

    /// <summary>Builds the alias map by composing the per-category seed sets.</summary>
    /// <returns>Frozen dictionary keyed on the shortcode.</returns>
    private static FrozenDictionary<string, string> BuildMap()
    {
        var seed = new Dictionary<string, string>(StringComparer.Ordinal);
        SeedSmileys(seed);
        SeedSymbols(seed);
        SeedHands(seed);
        SeedCelebration(seed);
        SeedStatus(seed);
        SeedDevEngineering(seed);
        SeedArrows(seed);
        return seed.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Registers smiley-face shortcodes.</summary>
    /// <param name="seed">Mutable seed dictionary.</param>
    private static void SeedSmileys(Dictionary<string, string> seed)
    {
        seed["smile"] = "😄";
        seed["grin"] = "😁";
        seed["joy"] = "😂";
        seed["rofl"] = "🤣";
        seed["wink"] = "😉";
        seed["blush"] = "😊";
        seed["heart_eyes"] = "😍";
        seed["thinking"] = "🤔";
        seed["sob"] = "😭";
        seed["cry"] = "😢";
        seed["scream"] = "😱";
        seed["angry"] = "😠";
        seed["rage"] = "😡";
        seed["sleeping"] = "😴";
        seed["sweat_smile"] = "😅";
        seed["smirk"] = "😏";
        seed["expressionless"] = "😑";
        seed["neutral_face"] = "😐";
    }

    /// <summary>Registers heart and visual-effect shortcodes.</summary>
    /// <param name="seed">Mutable seed dictionary.</param>
    private static void SeedSymbols(Dictionary<string, string> seed)
    {
        seed["heart"] = "❤️";
        seed["broken_heart"] = "💔";
        seed["sparkles"] = "✨";
        seed["fire"] = "🔥";
        seed["star"] = "⭐";
        seed["star2"] = "🌟";
        seed["dizzy"] = "💫";
        seed["100"] = "💯";
        seed["zzz"] = "💤";
        seed["boom"] = "💥";
    }

    /// <summary>Registers hand- and body-gesture shortcodes.</summary>
    /// <param name="seed">Mutable seed dictionary.</param>
    private static void SeedHands(Dictionary<string, string> seed)
    {
        seed["thumbsup"] = "👍";
        seed["thumbsdown"] = "👎";
        seed["+1"] = "👍";
        seed["-1"] = "👎";
        seed["wave"] = "👋";
        seed["clap"] = "👏";
        seed["pray"] = "🙏";
        seed["raised_hands"] = "🙌";
        seed["muscle"] = "💪";
        seed["ok_hand"] = "👌";
        seed["point_right"] = "👉";
        seed["point_left"] = "👈";
        seed["point_up"] = "👆";
        seed["point_down"] = "👇";
        seed["eyes"] = "👀";
        seed["see_no_evil"] = "🙈";
    }

    /// <summary>Registers celebration / award shortcodes.</summary>
    /// <param name="seed">Mutable seed dictionary.</param>
    private static void SeedCelebration(Dictionary<string, string> seed)
    {
        seed["tada"] = "🎉";
        seed["confetti_ball"] = "🎊";
        seed["balloon"] = "🎈";
        seed["gift"] = "🎁";
        seed["trophy"] = "🏆";
        seed["medal"] = "🏅";
        seed["rocket"] = "🚀";
    }

    /// <summary>Registers status / annotation shortcodes commonly used in docs and changelogs.</summary>
    /// <param name="seed">Mutable seed dictionary.</param>
    private static void SeedStatus(Dictionary<string, string> seed)
    {
        seed["warning"] = "⚠️";
        seed["white_check_mark"] = "✅";
        seed["heavy_check_mark"] = "✔️";
        seed["x"] = "❌";
        seed["heavy_multiplication_x"] = "✖️";
        seed["bangbang"] = "‼️";
        seed["question"] = "❓";
        seed["exclamation"] = "❗";
        seed["bulb"] = "💡";
        seed["information_source"] = "ℹ️";
        seed["rotating_light"] = "🚨";
        seed["no_entry"] = "⛔";
        seed["construction"] = "🚧";
    }

    /// <summary>Registers developer / engineering shortcodes.</summary>
    /// <param name="seed">Mutable seed dictionary.</param>
    private static void SeedDevEngineering(Dictionary<string, string> seed)
    {
        seed["bug"] = "🐛";
        seed["wrench"] = "🔧";
        seed["hammer"] = "🔨";
        seed["gear"] = "⚙️";
        seed["package"] = "📦";
        seed["computer"] = "💻";
        seed["floppy_disk"] = "💾";
        seed["lock"] = "🔒";
        seed["unlock"] = "🔓";
        seed["key"] = "🔑";
        seed["mag"] = "🔍";
        seed["memo"] = "📝";
        seed["books"] = "📚";
        seed["clipboard"] = "📋";
        seed["link"] = "🔗";
        seed["pushpin"] = "📌";
        seed["zap"] = "⚡";
        seed["recycle"] = "♻️";
    }

    /// <summary>Registers directional-arrow shortcodes.</summary>
    /// <param name="seed">Mutable seed dictionary.</param>
    private static void SeedArrows(Dictionary<string, string> seed)
    {
        seed["arrow_up"] = "⬆️";
        seed["arrow_down"] = "⬇️";
        seed["arrow_left"] = "⬅️";
        seed["arrow_right"] = "➡️";
        seed["arrows_clockwise"] = "🔃";
        seed["repeat"] = "🔁";
    }
}
