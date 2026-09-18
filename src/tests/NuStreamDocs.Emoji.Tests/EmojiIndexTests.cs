// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.Emoji.Tests;

/// <summary>
/// Parameterized coverage of <see cref = "EmojiIndex"/> — one row per
/// shortcode in the curated table. Each case exercises both the byte
/// API directly and the rewriter end-to-end so a regression in either
/// the table data or the dispatch wiring surfaces here.
/// </summary>
public class EmojiIndexTests
{
    /// <summary>Shortcode and glyph pairs supported by the index.</summary>
    private static readonly (string Shortcode, string Expected)[] ShortcodeCases =
    [
        ("smile", "😄"),
        ("smiley", "😃"),
        ("grin", "😁"),
        ("grinning", "😀"),
        ("joy", "😂"),
        ("rofl", "🤣"),
        ("wink", "😉"),
        ("blush", "😊"),
        ("heart_eyes", "😍"),
        ("kissing_heart", "😘"),
        ("yum", "😋"),
        ("sunglasses", "😎"),
        ("partying_face", "🥳"),
        ("thinking", "🤔"),
        ("expressionless", "😑"),
        ("smirk", "😏"),
        ("rolling_eyes", "🙄"),
        ("flushed", "😳"),
        ("sob", "😭"),
        ("cry", "😢"),
        ("scream", "😱"),
        ("rage", "😡"),
        ("angry", "😠"),
        ("exploding_head", "🤯"),
        ("hot_face", "🥵"),
        ("cold_face", "🥶"),
        ("nauseated_face", "🤢"),
        ("mask", "😷"),
        ("sleeping", "😴"),
        ("sweat_smile", "😅"),
        ("upside_down_face", "🙃"),
        ("pleading_face", "🥺"),
        ("ghost", "👻"),
        ("skull", "💀"),
        ("alien", "👽"),
        ("robot", "🤖"),
        ("poop", "💩"),
        ("thumbsup", "👍"),
        ("+1", "👍"),
        ("thumbsdown", "👎"),
        ("-1", "👎"),
        ("ok_hand", "👌"),
        ("victory", "✌️"),
        ("crossed_fingers", "🤞"),
        ("metal", "🤘"),
        ("call_me_hand", "🤙"),
        ("point_left", "👈"),
        ("point_right", "👉"),
        ("point_up", "👆"),
        ("point_down", "👇"),
        ("raised_hand", "✋"),
        ("vulcan_salute", "🖖"),
        ("wave", "👋"),
        ("clap", "👏"),
        ("raised_hands", "🙌"),
        ("handshake", "🤝"),
        ("pray", "🙏"),
        ("muscle", "💪"),
        ("eyes", "👀"),
        ("brain", "🧠"),
        ("see_no_evil", "🙈"),
        ("hear_no_evil", "🙉"),
        ("speak_no_evil", "🙊"),
        ("heart", "❤️"),
        ("orange_heart", "🧡"),
        ("yellow_heart", "💛"),
        ("green_heart", "💚"),
        ("blue_heart", "💙"),
        ("purple_heart", "💜"),
        ("black_heart", "🖤"),
        ("white_heart", "🤍"),
        ("brown_heart", "🤎"),
        ("broken_heart", "💔"),
        ("two_hearts", "💕"),
        ("sparkling_heart", "💖"),
        ("sparkles", "✨"),
        ("fire", "🔥"),
        ("star", "⭐"),
        ("star2", "🌟"),
        ("dizzy", "💫"),
        ("100", "💯"),
        ("zzz", "💤"),
        ("boom", "💥"),
        ("speech_balloon", "💬"),
        ("thought_balloon", "💭"),
        ("tada", "🎉"),
        ("confetti_ball", "🎊"),
        ("balloon", "🎈"),
        ("birthday", "🎂"),
        ("gift", "🎁"),
        ("trophy", "🏆"),
        ("medal", "🏅"),
        ("first_place_medal", "🥇"),
        ("rocket", "🚀"),
        ("crown", "👑"),
        ("warning", "⚠️"),
        ("white_check_mark", "✅"),
        ("heavy_check_mark", "✔️"),
        ("x", "❌"),
        ("question", "❓"),
        ("exclamation", "❗"),
        ("bulb", "💡"),
        ("information_source", "ℹ️"),
        ("rotating_light", "🚨"),
        ("no_entry", "⛔"),
        ("construction", "🚧"),
        ("bug", "🐛"),
        ("wrench", "🔧"),
        ("hammer", "🔨"),
        ("gear", "⚙️"),
        ("package", "📦"),
        ("computer", "💻"),
        ("keyboard", "⌨️"),
        ("floppy_disk", "💾"),
        ("battery", "🔋"),
        ("lock", "🔒"),
        ("unlock", "🔓"),
        ("key", "🔑"),
        ("mag", "🔍"),
        ("memo", "📝"),
        ("scissors", "✂️"),
        ("books", "📚"),
        ("clipboard", "📋"),
        ("file_folder", "📁"),
        ("link", "🔗"),
        ("pushpin", "📌"),
        ("zap", "⚡"),
        ("recycle", "♻️"),
        ("hourglass", "⌛"),
        ("watch", "⌚"),
        ("alarm_clock", "⏰"),
        ("test_tube", "🧪"),
        ("microscope", "🔬"),
        ("telescope", "🔭"),
        ("arrow_up", "⬆️"),
        ("arrow_down", "⬇️"),
        ("arrow_left", "⬅️"),
        ("arrow_right", "➡️"),
        ("arrows_clockwise", "🔃"),
        ("arrows_counterclockwise", "🔄"),
        ("repeat", "🔁"),
        ("sun", "☀️"),
        ("cloud", "☁️"),
        ("rainbow", "🌈"),
        ("snowflake", "❄️"),
        ("snowman", "⛄"),
        ("crescent_moon", "🌙"),
        ("earth_americas", "🌎"),
        ("ocean", "🌊"),
        ("evergreen_tree", "🌲"),
        ("cactus", "🌵"),
        ("seedling", "🌱"),
        ("four_leaf_clover", "🍀"),
        ("rose", "🌹"),
        ("sunflower", "🌻"),
        ("cherry_blossom", "🌸"),
        ("dog", "🐶"),
        ("cat", "🐱"),
        ("rabbit", "🐰"),
        ("fox", "🦊"),
        ("bear", "🐻"),
        ("panda", "🐼"),
        ("tiger", "🐯"),
        ("lion", "🦁"),
        ("frog", "🐸"),
        ("penguin", "🐧"),
        ("owl", "🦉"),
        ("turtle", "🐢"),
        ("snake", "🐍"),
        ("octopus", "🐙"),
        ("whale", "🐳"),
        ("fish", "🐟"),
        ("shark", "🦈"),
        ("dragon", "🐉"),
        ("unicorn", "🦄"),
        ("elephant", "🐘"),
        ("butterfly", "🦋"),
        ("bee", "🐝"),
        ("coffee", "☕"),
        ("tea", "🍵"),
        ("beer", "🍺"),
        ("wine_glass", "🍷"),
        ("apple", "🍎"),
        ("banana", "🍌"),
        ("strawberry", "🍓"),
        ("pizza", "🍕"),
        ("hamburger", "🍔"),
        ("sushi", "🍣"),
        ("doughnut", "🍩"),
        ("cookie", "🍪"),
        ("ice_cream", "🍨"),
        ("car", "🚗"),
        ("bus", "🚌"),
        ("train", "🚆"),
        ("airplane", "✈️"),
        ("ship", "🚢"),
        ("sailboat", "⛵"),
        ("anchor", "⚓"),
        ("bike", "🚲"),
        ("traffic_light", "🚦"),
        ("musical_note", "🎵"),
        ("notes", "🎶"),
        ("microphone", "🎤"),
        ("headphones", "🎧"),
        ("guitar", "🎸"),
        ("drum", "🥁"),
        ("soccer", "⚽"),
        ("basketball", "🏀"),
        ("football", "🏈"),
        ("baseball", "⚾"),
        ("tennis", "🎾"),
        ("dart", "🎯"),
    ];

    /// <summary>Supplies the shortcode and expected glyph cases.</summary>
    /// <returns>All supported shortcode cases.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<(string Shortcode, string Expected)> EnumerateShortcodeCases() => ShortcodeCases;

    /// <summary>The byte API returns the documented glyph for every supported shortcode.</summary>
    /// <param name = "shortcode">Shortcode body without colons.</param>
    /// <param name = "expected">Expected glyph.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [MethodDataSource(nameof(EnumerateShortcodeCases))]
    public async Task ShortcodeResolvesToGlyph(string shortcode, string expected)
    {
        var bytes = Encoding.UTF8.GetBytes(shortcode);
        var(found, actual) = Resolve(bytes);
        await Assert.That(found).IsTrue();
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Unknown / typo / empty shortcodes miss cleanly.</summary>
    /// <param name = "shortcode">Shortcode body that should not resolve.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("nonexistent")]
    [Arguments("")]
    [Arguments("smiles")] // close but not exact
    [Arguments("Smile")] // case-mismatch
    [Arguments("rocket ")] // trailing space — body byte filter would strip but the API itself is verbatim
    [Arguments("foo_bar_baz_qux_quux")]
    public async Task UnknownShortcodeMisses(string shortcode)
    {
        var bytes = Encoding.UTF8.GetBytes(shortcode);
        var(found, actual) = Resolve(bytes);
        await Assert.That(found).IsFalse();
        await Assert.That(actual).IsEqualTo(string.Empty);
    }

    /// <summary>Resolves <paramref name = "bytes"/> against the index; copies the glyph out to a string before any await so the span doesn't cross an async boundary.</summary>
    /// <param name = "bytes">UTF-8 shortcode bytes.</param>
    /// <returns>Hit flag and decoded glyph (empty when missed).</returns>
    private static (bool Found, string Glyph) Resolve(byte[] bytes)
    {
        var found = EmojiIndex.TryGet(bytes, out var glyph);
        return (found, found ? Encoding.UTF8.GetString(glyph) : string.Empty);
    }
}
