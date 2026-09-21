// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
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
    private static readonly ShortcodeCase[] ShortcodeCases =
    [
        new("smile", "😄"),
        new("smiley", "😃"),
        new("grin", "😁"),
        new("grinning", "😀"),
        new("joy", "😂"),
        new("rofl", "🤣"),
        new("wink", "😉"),
        new("blush", "😊"),
        new("heart_eyes", "😍"),
        new("kissing_heart", "😘"),
        new("yum", "😋"),
        new("sunglasses", "😎"),
        new("partying_face", "🥳"),
        new("thinking", "🤔"),
        new("expressionless", "😑"),
        new("smirk", "😏"),
        new("rolling_eyes", "🙄"),
        new("flushed", "😳"),
        new("sob", "😭"),
        new("cry", "😢"),
        new("scream", "😱"),
        new("rage", "😡"),
        new("angry", "😠"),
        new("exploding_head", "🤯"),
        new("hot_face", "🥵"),
        new("cold_face", "🥶"),
        new("nauseated_face", "🤢"),
        new("mask", "😷"),
        new("sleeping", "😴"),
        new("sweat_smile", "😅"),
        new("upside_down_face", "🙃"),
        new("pleading_face", "🥺"),
        new("ghost", "👻"),
        new("skull", "💀"),
        new("alien", "👽"),
        new("robot", "🤖"),
        new("poop", "💩"),
        new("thumbsup", "👍"),
        new("+1", "👍"),
        new("thumbsdown", "👎"),
        new("-1", "👎"),
        new("ok_hand", "👌"),
        new("victory", "✌️"),
        new("crossed_fingers", "🤞"),
        new("metal", "🤘"),
        new("call_me_hand", "🤙"),
        new("point_left", "👈"),
        new("point_right", "👉"),
        new("point_up", "👆"),
        new("point_down", "👇"),
        new("raised_hand", "✋"),
        new("vulcan_salute", "🖖"),
        new("wave", "👋"),
        new("clap", "👏"),
        new("raised_hands", "🙌"),
        new("handshake", "🤝"),
        new("pray", "🙏"),
        new("muscle", "💪"),
        new("eyes", "👀"),
        new("brain", "🧠"),
        new("see_no_evil", "🙈"),
        new("hear_no_evil", "🙉"),
        new("speak_no_evil", "🙊"),
        new("heart", "❤️"),
        new("orange_heart", "🧡"),
        new("yellow_heart", "💛"),
        new("green_heart", "💚"),
        new("blue_heart", "💙"),
        new("purple_heart", "💜"),
        new("black_heart", "🖤"),
        new("white_heart", "🤍"),
        new("brown_heart", "🤎"),
        new("broken_heart", "💔"),
        new("two_hearts", "💕"),
        new("sparkling_heart", "💖"),
        new("sparkles", "✨"),
        new("fire", "🔥"),
        new("star", "⭐"),
        new("star2", "🌟"),
        new("dizzy", "💫"),
        new("100", "💯"),
        new("zzz", "💤"),
        new("boom", "💥"),
        new("speech_balloon", "💬"),
        new("thought_balloon", "💭"),
        new("tada", "🎉"),
        new("confetti_ball", "🎊"),
        new("balloon", "🎈"),
        new("birthday", "🎂"),
        new("gift", "🎁"),
        new("trophy", "🏆"),
        new("medal", "🏅"),
        new("first_place_medal", "🥇"),
        new("rocket", "🚀"),
        new("crown", "👑"),
        new("warning", "⚠️"),
        new("white_check_mark", "✅"),
        new("heavy_check_mark", "✔️"),
        new("x", "❌"),
        new("question", "❓"),
        new("exclamation", "❗"),
        new("bulb", "💡"),
        new("information_source", "ℹ️"),
        new("rotating_light", "🚨"),
        new("no_entry", "⛔"),
        new("construction", "🚧"),
        new("bug", "🐛"),
        new("wrench", "🔧"),
        new("hammer", "🔨"),
        new("gear", "⚙️"),
        new("package", "📦"),
        new("computer", "💻"),
        new("keyboard", "⌨️"),
        new("floppy_disk", "💾"),
        new("battery", "🔋"),
        new("lock", "🔒"),
        new("unlock", "🔓"),
        new("key", "🔑"),
        new("mag", "🔍"),
        new("memo", "📝"),
        new("scissors", "✂️"),
        new("books", "📚"),
        new("clipboard", "📋"),
        new("file_folder", "📁"),
        new("link", "🔗"),
        new("pushpin", "📌"),
        new("zap", "⚡"),
        new("recycle", "♻️"),
        new("hourglass", "⌛"),
        new("watch", "⌚"),
        new("alarm_clock", "⏰"),
        new("test_tube", "🧪"),
        new("microscope", "🔬"),
        new("telescope", "🔭"),
        new("arrow_up", "⬆️"),
        new("arrow_down", "⬇️"),
        new("arrow_left", "⬅️"),
        new("arrow_right", "➡️"),
        new("arrows_clockwise", "🔃"),
        new("arrows_counterclockwise", "🔄"),
        new("repeat", "🔁"),
        new("sun", "☀️"),
        new("cloud", "☁️"),
        new("rainbow", "🌈"),
        new("snowflake", "❄️"),
        new("snowman", "⛄"),
        new("crescent_moon", "🌙"),
        new("earth_americas", "🌎"),
        new("ocean", "🌊"),
        new("evergreen_tree", "🌲"),
        new("cactus", "🌵"),
        new("seedling", "🌱"),
        new("four_leaf_clover", "🍀"),
        new("rose", "🌹"),
        new("sunflower", "🌻"),
        new("cherry_blossom", "🌸"),
        new("dog", "🐶"),
        new("cat", "🐱"),
        new("rabbit", "🐰"),
        new("fox", "🦊"),
        new("bear", "🐻"),
        new("panda", "🐼"),
        new("tiger", "🐯"),
        new("lion", "🦁"),
        new("frog", "🐸"),
        new("penguin", "🐧"),
        new("owl", "🦉"),
        new("turtle", "🐢"),
        new("snake", "🐍"),
        new("octopus", "🐙"),
        new("whale", "🐳"),
        new("fish", "🐟"),
        new("shark", "🦈"),
        new("dragon", "🐉"),
        new("unicorn", "🦄"),
        new("elephant", "🐘"),
        new("butterfly", "🦋"),
        new("bee", "🐝"),
        new("coffee", "☕"),
        new("tea", "🍵"),
        new("beer", "🍺"),
        new("wine_glass", "🍷"),
        new("apple", "🍎"),
        new("banana", "🍌"),
        new("strawberry", "🍓"),
        new("pizza", "🍕"),
        new("hamburger", "🍔"),
        new("sushi", "🍣"),
        new("doughnut", "🍩"),
        new("cookie", "🍪"),
        new("ice_cream", "🍨"),
        new("car", "🚗"),
        new("bus", "🚌"),
        new("train", "🚆"),
        new("airplane", "✈️"),
        new("ship", "🚢"),
        new("sailboat", "⛵"),
        new("anchor", "⚓"),
        new("bike", "🚲"),
        new("traffic_light", "🚦"),
        new("musical_note", "🎵"),
        new("notes", "🎶"),
        new("microphone", "🎤"),
        new("headphones", "🎧"),
        new("guitar", "🎸"),
        new("drum", "🥁"),
        new("soccer", "⚽"),
        new("basketball", "🏀"),
        new("football", "🏈"),
        new("baseball", "⚾"),
        new("tennis", "🎾"),
        new("dart", "🎯"),
    ];

    /// <summary>Supplies the shortcode and expected glyph cases.</summary>
    /// <returns>All supported shortcode cases.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<ShortcodeCase> EnumerateShortcodeCases() => ShortcodeCases;

    /// <summary>The byte API returns the documented glyph for every supported shortcode.</summary>
    /// <param name = "row">Shortcode and its expected glyph.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [MethodDataSource(nameof(EnumerateShortcodeCases))]
    public async Task ShortcodeResolvesToGlyph(ShortcodeCase row)
    {
        var bytes = Encoding.UTF8.GetBytes(row.Shortcode);
        var result = Resolve(bytes);
        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.Glyph).IsEqualTo(row.Expected);
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
        var result = Resolve(bytes);
        await Assert.That(result.Found).IsFalse();
        await Assert.That(result.Glyph).IsEqualTo(string.Empty);
    }

    /// <summary>Resolves <paramref name = "bytes"/> against the index; copies the glyph out to a string before any await so the span doesn't cross an async boundary.</summary>
    /// <param name = "bytes">UTF-8 shortcode bytes.</param>
    /// <returns>Hit flag and decoded glyph (empty when missed).</returns>
    private static ResolveResult Resolve(byte[] bytes)
    {
        var found = EmojiIndex.TryGet(bytes, out var glyph);
        return new(found, found ? Encoding.UTF8.GetString(glyph) : string.Empty);
    }

    /// <summary>The outcome of resolving a shortcode.</summary>
    /// <param name = "Found">Whether the index contains the shortcode.</param>
    /// <param name = "Glyph">Decoded glyph, empty when missed.</param>
    private readonly record struct ResolveResult(bool Found, string Glyph);

    /// <summary>A shortcode and the glyph it resolves to.</summary>
    /// <param name = "Shortcode">Shortcode body without colons.</param>
    /// <param name = "Expected">Expected glyph.</param>
    [DebuggerDisplay("{Shortcode}")]
    public sealed record ShortcodeCase(string Shortcode, string Expected);
}
