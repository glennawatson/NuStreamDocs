// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;

namespace NuStreamDocs.Fonts;

/// <summary>Builds the <c>"&lt;family&gt; fallback"</c> <c>@font-face</c> rule so the swap to the webfont causes no layout shift.</summary>
public static class FallbackFaceBuilder
{
    /// <summary>Maximum digits a formatted percentage can need.</summary>
    private const int PercentBufferSize = 24;

    /// <summary>Multiplier turning a fraction into a percentage.</summary>
    private const double PercentScale = 100.0;

    /// <summary>Computes the CLS override fractions aligning the system reference font for <paramref name="generic"/> to the metrics in <paramref name="web"/>.</summary>
    /// <param name="web">The webfont's metrics.</param>
    /// <param name="generic">The generic fallback family (selects the reference font).</param>
    /// <returns>The override fractions.</returns>
    public static Overrides ComputeOverrides(in FontMetrics web, GenericFontFamily generic)
    {
        var fallback = ReferenceFontMetrics.ForGeneric(generic);
        var webXRatio = (double)web.XHeight / web.UnitsPerEm;
        var fallbackXRatio = (double)fallback.XHeight / fallback.UnitsPerEm;
        var sizeAdjust = web.XHeight > 0 && fallback.XHeight > 0 ? webXRatio / fallbackXRatio : 1.0;
        var em = (double)web.UnitsPerEm;
        return new(
            sizeAdjust,
            web.Ascender / em / sizeAdjust,
            Math.Abs(web.Descender) / em / sizeAdjust,
            web.LineGap / em / sizeAdjust);
    }

    /// <summary>Writes the fallback <c>@font-face</c> rule for <paramref name="familyBytes"/> to <paramref name="writer"/>.</summary>
    /// <param name="familyBytes">UTF-8 webfont family name.</param>
    /// <param name="web">The webfont's metrics.</param>
    /// <param name="generic">The generic fallback family.</param>
    /// <param name="writer">Destination.</param>
    public static void Write(ReadOnlySpan<byte> familyBytes, in FontMetrics web, GenericFontFamily generic, IBufferWriter<byte> writer)
    {
        var o = ComputeOverrides(web, generic);
        writer.Write("@font-face{font-family:\""u8);
        writer.Write(familyBytes);
        writer.Write(" fallback"u8);
        writer.Write("\";src:"u8);
        writer.Write(ReferenceFontMetrics.LocalSourcesFor(generic));
        writer.Write(";ascent-override:"u8);
        WritePercent(writer, o.AscentOverride);
        writer.Write(";descent-override:"u8);
        WritePercent(writer, o.DescentOverride);
        writer.Write(";line-gap-override:"u8);
        WritePercent(writer, o.LineGapOverride);
        writer.Write(";size-adjust:"u8);
        WritePercent(writer, o.SizeAdjust);
        writer.Write(";}"u8);
    }

    /// <summary>Writes <paramref name="fraction"/> as a percentage with up to three fractional digits.</summary>
    /// <param name="writer">Destination.</param>
    /// <param name="fraction">Fraction (1.0 = 100%).</param>
    private static void WritePercent(IBufferWriter<byte> writer, double fraction)
    {
        Span<byte> buffer = stackalloc byte[PercentBufferSize];
        if (!(fraction * PercentScale).TryFormat(buffer, out var written, "0.###", CultureInfo.InvariantCulture))
        {
            written = 1;
            buffer[0] = (byte)'0';
        }

        writer.Write(buffer[..written]);
        writer.Write("%"u8);
    }

    /// <summary>Computed CLS override fractions for one face (1.0 = 100%).</summary>
    /// <param name="SizeAdjust">The <c>size-adjust</c> fraction.</param>
    /// <param name="AscentOverride">The <c>ascent-override</c> fraction.</param>
    /// <param name="DescentOverride">The <c>descent-override</c> fraction.</param>
    /// <param name="LineGapOverride">The <c>line-gap-override</c> fraction.</param>
    public readonly record struct Overrides(double SizeAdjust, double AscentOverride, double DescentOverride, double LineGapOverride);
}
