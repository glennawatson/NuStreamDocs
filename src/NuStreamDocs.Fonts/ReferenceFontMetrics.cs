// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts;

/// <summary>Metrics and <c>src: local(...)</c> stacks for the system reference fonts used to build CLS fallback faces.</summary>
/// <remarks>Metric values follow the <c>@capsizecss/metrics</c> dataset for Arial, Times New Roman, and Courier New.</remarks>
internal static class ReferenceFontMetrics
{
    /// <summary>Arial metrics (the sans-serif reference).</summary>
    private static readonly FontMetrics Arial = new(2048, 1854, -434, 67, 1062, 1467);

    /// <summary>Times New Roman metrics (the serif reference).</summary>
    private static readonly FontMetrics TimesNewRoman = new(2048, 1825, -443, 87, 916, 1356);

    /// <summary>Courier New metrics (the monospace reference).</summary>
    private static readonly FontMetrics CourierNew = new(2048, 1705, -615, 0, 866, 1170);

    /// <summary>UTF-8 <c>src</c> value listing local sans-serif system fonts.</summary>
    private static readonly byte[] SansSrc = [.. "local(\"Arial\"), local(\"Helvetica Neue\"), local(\"Helvetica\"), local(\"Liberation Sans\"), local(\"Arimo\")"u8];

    /// <summary>UTF-8 <c>src</c> value listing local serif system fonts.</summary>
    private static readonly byte[] SerifSrc = [.. "local(\"Times New Roman\"), local(\"Times\"), local(\"Liberation Serif\"), local(\"Tinos\")"u8];

    /// <summary>UTF-8 <c>src</c> value listing local monospace system fonts.</summary>
    private static readonly byte[] MonoSrc = [.. "local(\"Courier New\"), local(\"Courier\"), local(\"Liberation Mono\"), local(\"Cousine\")"u8];

    /// <summary>Returns the reference-font metrics for the given generic family.</summary>
    /// <param name="generic">Generic font family.</param>
    /// <returns>The reference metrics.</returns>
    public static FontMetrics ForGeneric(GenericFontFamily generic) => generic switch
    {
        GenericFontFamily.Serif => TimesNewRoman,
        GenericFontFamily.Monospace => CourierNew,
        _ => Arial,
    };

    /// <summary>Returns the UTF-8 <c>src: local(...)</c> stack for the given generic family.</summary>
    /// <param name="generic">Generic font family.</param>
    /// <returns>The <c>src</c> bytes.</returns>
    public static ReadOnlySpan<byte> LocalSourcesFor(GenericFontFamily generic) => generic switch
    {
        GenericFontFamily.Serif => SerifSrc,
        GenericFontFamily.Monospace => MonoSrc,
        _ => SansSrc,
    };

    /// <summary>Returns the UTF-8 generic-family keyword (<c>sans-serif</c> / <c>serif</c> / <c>monospace</c>).</summary>
    /// <param name="generic">Generic font family.</param>
    /// <returns>The keyword bytes.</returns>
    public static ReadOnlySpan<byte> KeywordFor(GenericFontFamily generic) => generic switch
    {
        GenericFontFamily.Serif => "serif"u8,
        GenericFontFamily.Monospace => "monospace"u8,
        _ => "sans-serif"u8,
    };
}
