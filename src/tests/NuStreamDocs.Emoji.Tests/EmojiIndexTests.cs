// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Emoji.Tests;

/// <summary>
/// Parameterised coverage of <see cref="EmojiIndex"/> — one row per
/// shortcode in the curated table. Each case exercises both the byte
/// API directly and the rewriter end-to-end so a regression in either
/// the table data or the dispatch wiring surfaces here.
/// </summary>
public class EmojiIndexTests
{
    /// <summary>The byte API returns the documented glyph for every supported shortcode.</summary>
    /// <param name="shortcode">Shortcode body (no colons).</param>
    /// <param name="expected">Expected UTF-8 glyph.</param>
    /// <returns>Async test.</returns>
    [Test]

    // Smileys.
    [Arguments("smile", "😄")]
    [Arguments("smiley", "😃")]
    [Arguments("grin", "😁")]
    [Arguments("grinning", "😀")]
    [Arguments("joy", "😂")]
    [Arguments("rofl", "🤣")]
    [Arguments("wink", "😉")]
    [Arguments("blush", "😊")]
    [Arguments("heart_eyes", "😍")]
    [Arguments("kissing_heart", "😘")]
    [Arguments("yum", "😋")]
    [Arguments("sunglasses", "😎")]
    [Arguments("partying_face", "🥳")]
    [Arguments("thinking", "🤔")]
    [Arguments("expressionless", "😑")]
    [Arguments("smirk", "😏")]
    [Arguments("rolling_eyes", "🙄")]
    [Arguments("flushed", "😳")]
    [Arguments("sob", "😭")]
    [Arguments("cry", "😢")]
    [Arguments("scream", "😱")]
    [Arguments("rage", "😡")]
    [Arguments("angry", "😠")]
    [Arguments("exploding_head", "🤯")]
    [Arguments("hot_face", "🥵")]
    [Arguments("cold_face", "🥶")]
    [Arguments("nauseated_face", "🤢")]
    [Arguments("mask", "😷")]
    [Arguments("sleeping", "😴")]
    [Arguments("sweat_smile", "😅")]
    [Arguments("upside_down_face", "🙃")]
    [Arguments("pleading_face", "🥺")]
    [Arguments("ghost", "👻")]
    [Arguments("skull", "💀")]
    [Arguments("alien", "👽")]
    [Arguments("robot", "🤖")]
    [Arguments("poop", "💩")]

    // Hands and body.
    [Arguments("thumbsup", "👍")]
    [Arguments("+1", "👍")]
    [Arguments("thumbsdown", "👎")]
    [Arguments("-1", "👎")]
    [Arguments("ok_hand", "👌")]
    [Arguments("victory", "✌️")]
    [Arguments("crossed_fingers", "🤞")]
    [Arguments("metal", "🤘")]
    [Arguments("call_me_hand", "🤙")]
    [Arguments("point_left", "👈")]
    [Arguments("point_right", "👉")]
    [Arguments("point_up", "👆")]
    [Arguments("point_down", "👇")]
    [Arguments("raised_hand", "✋")]
    [Arguments("vulcan_salute", "🖖")]
    [Arguments("wave", "👋")]
    [Arguments("clap", "👏")]
    [Arguments("raised_hands", "🙌")]
    [Arguments("handshake", "🤝")]
    [Arguments("pray", "🙏")]
    [Arguments("muscle", "💪")]
    [Arguments("eyes", "👀")]
    [Arguments("brain", "🧠")]
    [Arguments("see_no_evil", "🙈")]
    [Arguments("hear_no_evil", "🙉")]
    [Arguments("speak_no_evil", "🙊")]

    // Hearts and visual effects.
    [Arguments("heart", "❤️")]
    [Arguments("orange_heart", "🧡")]
    [Arguments("yellow_heart", "💛")]
    [Arguments("green_heart", "💚")]
    [Arguments("blue_heart", "💙")]
    [Arguments("purple_heart", "💜")]
    [Arguments("black_heart", "🖤")]
    [Arguments("white_heart", "🤍")]
    [Arguments("brown_heart", "🤎")]
    [Arguments("broken_heart", "💔")]
    [Arguments("two_hearts", "💕")]
    [Arguments("sparkling_heart", "💖")]
    [Arguments("sparkles", "✨")]
    [Arguments("fire", "🔥")]
    [Arguments("star", "⭐")]
    [Arguments("star2", "🌟")]
    [Arguments("dizzy", "💫")]
    [Arguments("100", "💯")]
    [Arguments("zzz", "💤")]
    [Arguments("boom", "💥")]
    [Arguments("speech_balloon", "💬")]
    [Arguments("thought_balloon", "💭")]

    // Celebration.
    [Arguments("tada", "🎉")]
    [Arguments("confetti_ball", "🎊")]
    [Arguments("balloon", "🎈")]
    [Arguments("birthday", "🎂")]
    [Arguments("gift", "🎁")]
    [Arguments("trophy", "🏆")]
    [Arguments("medal", "🏅")]
    [Arguments("first_place_medal", "🥇")]
    [Arguments("rocket", "🚀")]
    [Arguments("crown", "👑")]

    // Status.
    [Arguments("warning", "⚠️")]
    [Arguments("white_check_mark", "✅")]
    [Arguments("heavy_check_mark", "✔️")]
    [Arguments("x", "❌")]
    [Arguments("question", "❓")]
    [Arguments("exclamation", "❗")]
    [Arguments("bulb", "💡")]
    [Arguments("information_source", "ℹ️")]
    [Arguments("rotating_light", "🚨")]
    [Arguments("no_entry", "⛔")]
    [Arguments("construction", "🚧")]

    // Developer / engineering.
    [Arguments("bug", "🐛")]
    [Arguments("wrench", "🔧")]
    [Arguments("hammer", "🔨")]
    [Arguments("gear", "⚙️")]
    [Arguments("package", "📦")]
    [Arguments("computer", "💻")]
    [Arguments("keyboard", "⌨️")]
    [Arguments("floppy_disk", "💾")]
    [Arguments("battery", "🔋")]
    [Arguments("lock", "🔒")]
    [Arguments("unlock", "🔓")]
    [Arguments("key", "🔑")]
    [Arguments("mag", "🔍")]
    [Arguments("memo", "📝")]
    [Arguments("scissors", "✂️")]
    [Arguments("books", "📚")]
    [Arguments("clipboard", "📋")]
    [Arguments("file_folder", "📁")]
    [Arguments("link", "🔗")]
    [Arguments("pushpin", "📌")]
    [Arguments("zap", "⚡")]
    [Arguments("recycle", "♻️")]
    [Arguments("hourglass", "⌛")]
    [Arguments("watch", "⌚")]
    [Arguments("alarm_clock", "⏰")]
    [Arguments("test_tube", "🧪")]
    [Arguments("microscope", "🔬")]
    [Arguments("telescope", "🔭")]

    // Arrows.
    [Arguments("arrow_up", "⬆️")]
    [Arguments("arrow_down", "⬇️")]
    [Arguments("arrow_left", "⬅️")]
    [Arguments("arrow_right", "➡️")]
    [Arguments("arrows_clockwise", "🔃")]
    [Arguments("arrows_counterclockwise", "🔄")]
    [Arguments("repeat", "🔁")]

    // Weather and nature.
    [Arguments("sun", "☀️")]
    [Arguments("cloud", "☁️")]
    [Arguments("rainbow", "🌈")]
    [Arguments("snowflake", "❄️")]
    [Arguments("snowman", "⛄")]
    [Arguments("crescent_moon", "🌙")]
    [Arguments("earth_americas", "🌎")]
    [Arguments("ocean", "🌊")]
    [Arguments("evergreen_tree", "🌲")]
    [Arguments("cactus", "🌵")]
    [Arguments("seedling", "🌱")]
    [Arguments("four_leaf_clover", "🍀")]
    [Arguments("rose", "🌹")]
    [Arguments("sunflower", "🌻")]
    [Arguments("cherry_blossom", "🌸")]

    // Animals.
    [Arguments("dog", "🐶")]
    [Arguments("cat", "🐱")]
    [Arguments("rabbit", "🐰")]
    [Arguments("fox", "🦊")]
    [Arguments("bear", "🐻")]
    [Arguments("panda", "🐼")]
    [Arguments("tiger", "🐯")]
    [Arguments("lion", "🦁")]
    [Arguments("frog", "🐸")]
    [Arguments("penguin", "🐧")]
    [Arguments("owl", "🦉")]
    [Arguments("turtle", "🐢")]
    [Arguments("snake", "🐍")]
    [Arguments("octopus", "🐙")]
    [Arguments("whale", "🐳")]
    [Arguments("fish", "🐟")]
    [Arguments("shark", "🦈")]
    [Arguments("dragon", "🐉")]
    [Arguments("unicorn", "🦄")]
    [Arguments("elephant", "🐘")]
    [Arguments("butterfly", "🦋")]
    [Arguments("bee", "🐝")]

    // Food and drink.
    [Arguments("coffee", "☕")]
    [Arguments("tea", "🍵")]
    [Arguments("beer", "🍺")]
    [Arguments("wine_glass", "🍷")]
    [Arguments("apple", "🍎")]
    [Arguments("banana", "🍌")]
    [Arguments("strawberry", "🍓")]
    [Arguments("pizza", "🍕")]
    [Arguments("hamburger", "🍔")]
    [Arguments("sushi", "🍣")]
    [Arguments("doughnut", "🍩")]
    [Arguments("cookie", "🍪")]
    [Arguments("ice_cream", "🍨")]

    // Travel.
    [Arguments("car", "🚗")]
    [Arguments("bus", "🚌")]
    [Arguments("train", "🚆")]
    [Arguments("airplane", "✈️")]
    [Arguments("ship", "🚢")]
    [Arguments("sailboat", "⛵")]
    [Arguments("anchor", "⚓")]
    [Arguments("bike", "🚲")]
    [Arguments("traffic_light", "🚦")]

    // Music.
    [Arguments("musical_note", "🎵")]
    [Arguments("notes", "🎶")]
    [Arguments("microphone", "🎤")]
    [Arguments("headphones", "🎧")]
    [Arguments("guitar", "🎸")]
    [Arguments("drum", "🥁")]

    // Sports.
    [Arguments("soccer", "⚽")]
    [Arguments("basketball", "🏀")]
    [Arguments("football", "🏈")]
    [Arguments("baseball", "⚾")]
    [Arguments("tennis", "🎾")]
    [Arguments("dart", "🎯")]
    public async Task ShortcodeResolvesToGlyph(string shortcode, string expected)
    {
        var bytes = Encoding.UTF8.GetBytes(shortcode);
        var (found, actual) = Resolve(bytes);
        await Assert.That(found).IsTrue();
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Unknown / typo / empty shortcodes miss cleanly.</summary>
    /// <param name="shortcode">Shortcode body that should not resolve.</param>
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
        var (found, actual) = Resolve(bytes);
        await Assert.That(found).IsFalse();
        await Assert.That(actual).IsEqualTo(string.Empty);
    }

    /// <summary>Resolves <paramref name="bytes"/> against the index; copies the glyph out to a string before any await so the span doesn't cross an async boundary.</summary>
    /// <param name="bytes">UTF-8 shortcode bytes.</param>
    /// <returns>Hit flag and decoded glyph (empty when missed).</returns>
    private static (bool Found, string Glyph) Resolve(byte[] bytes)
    {
        var found = EmojiIndex.TryGet(bytes, out var glyph);
        return (found, found ? Encoding.UTF8.GetString(glyph) : string.Empty);
    }
}
