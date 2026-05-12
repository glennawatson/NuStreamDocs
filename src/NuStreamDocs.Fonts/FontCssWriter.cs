// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;

namespace NuStreamDocs.Fonts;

/// <summary>Assembles the generated <c>fonts.css</c>: an <c>@font-face</c> per resolved file, the CLS fallback faces, and a <c>:root</c> block wiring the theme font variables.</summary>
public static class FontCssWriter
{
    /// <summary>Maximum digits an int weight needs.</summary>
    private const int WeightBufferSize = 8;

    /// <summary>Writes the stylesheet for <paramref name="faces"/> to <paramref name="writer"/>.</summary>
    /// <param name="faces">Per-face inputs (already resolved, with metrics and asset paths).</param>
    /// <param name="writer">Destination.</param>
    public static void Write(ReadOnlySpan<FaceCss> faces, IBufferWriter<byte> writer)
    {
        for (var i = 0; i < faces.Length; i++)
        {
            WriteFaceRules(faces[i], writer);
        }

        writer.Write(":root{"u8);
        for (var i = 0; i < faces.Length; i++)
        {
            WriteRootVariables(faces[i], writer);
        }

        writer.Write("}"u8);
    }

    /// <summary>Writes the <c>@font-face</c> rules (real faces + the CLS fallback face) for one declared family.</summary>
    /// <param name="face">The face input.</param>
    /// <param name="writer">Destination.</param>
    private static void WriteFaceRules(in FaceCss face, IBufferWriter<byte> writer)
    {
        for (var i = 0; i < face.Resources.Length; i++)
        {
            var r = face.Resources[i];
            writer.Write("@font-face{font-family:\""u8);
            writer.Write(face.FamilyBytes);
            writer.Write("\";font-style:"u8);
            writer.Write(r.Style == FontStyle.Italic ? "italic"u8 : "normal"u8);
            writer.Write(";font-weight:"u8);
            WriteInt(writer, r.Weight);
            if (r.UnicodeRange is { Length: > 0 })
            {
                writer.Write(";unicode-range:"u8);
                writer.Write(r.UnicodeRange);
            }

            writer.Write(";font-display:"u8);
            writer.Write(DisplayKeyword(face.Display));
            writer.Write(";src:url(\"/"u8);
            writer.Write(r.AssetPath);
            writer.Write("\") format(\"woff2\");}"u8);
        }

        if (!face.Metrics.HasValue)
        {
            return;
        }

        FallbackFaceBuilder.Write(face.FamilyBytes, face.Metrics.Value, face.Fallback, writer);
    }

    /// <summary>Writes the <c>--nstd-font-&lt;id&gt;</c> declaration and the theme-variable aliases for one face.</summary>
    /// <param name="face">The face input.</param>
    /// <param name="writer">Destination.</param>
    private static void WriteRootVariables(in FaceCss face, IBufferWriter<byte> writer)
    {
        writer.Write("--nstd-font-"u8);
        writer.Write(face.Id);
        writer.Write(":\""u8);
        writer.Write(face.FamilyBytes);
        writer.Write("\",\""u8);
        writer.Write(face.FamilyBytes);
        writer.Write(" fallback\","u8);
        writer.Write(ReferenceFontMetrics.KeywordFor(face.Fallback));
        writer.Write(";"u8);
        for (var i = 0; i < face.ThemeVariables.Length; i++)
        {
            writer.Write(face.ThemeVariables[i]);
            writer.Write(":var(--nstd-font-"u8);
            writer.Write(face.Id);
            writer.Write(");"u8);
        }
    }

    /// <summary>Writes <paramref name="value"/> as decimal digits.</summary>
    /// <param name="writer">Destination.</param>
    /// <param name="value">The integer to write.</param>
    private static void WriteInt(IBufferWriter<byte> writer, int value)
    {
        Span<byte> buffer = stackalloc byte[WeightBufferSize];
        if (!Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            return;
        }

        writer.Write(buffer[..written]);
    }

    /// <summary>Maps a <see cref="FontDisplay"/> to its CSS keyword bytes.</summary>
    /// <param name="display">The display mode.</param>
    /// <returns>The keyword bytes.</returns>
    private static ReadOnlySpan<byte> DisplayKeyword(FontDisplay display) => display switch
    {
        FontDisplay.Block => "block"u8,
        FontDisplay.Swap => "swap"u8,
        FontDisplay.Fallback => "fallback"u8,
        FontDisplay.Optional => "optional"u8,
        _ => "auto"u8,
    };

    /// <summary>One resolved font file to emit as an <c>@font-face</c> rule.</summary>
    /// <param name="Weight">Numeric font weight.</param>
    /// <param name="Style">Upright or italic.</param>
    /// <param name="UnicodeRange">UTF-8 <c>unicode-range</c> value (empty to omit the descriptor).</param>
    /// <param name="AssetPath">Site-relative path the file is written to (without a leading slash).</param>
    public readonly record struct ResourceCss(int Weight, FontStyle Style, byte[] UnicodeRange, byte[] AssetPath);

    /// <summary>One declared family's contribution to the stylesheet.</summary>
    /// <param name="Id">UTF-8 identifier for the <c>--nstd-font-&lt;id&gt;</c> variable.</param>
    /// <param name="FamilyBytes">UTF-8 CSS family name.</param>
    /// <param name="Display">CSS <c>font-display</c> descriptor.</param>
    /// <param name="Fallback">Generic fallback family / keyword.</param>
    /// <param name="ThemeVariables">UTF-8 names of CSS custom properties to alias to <c>--nstd-font-&lt;id&gt;</c>.</param>
    /// <param name="Metrics">Webfont metrics for the CLS fallback face, or <see langword="null"/> to skip it.</param>
    /// <param name="Resources">The resolved files.</param>
    public readonly record struct FaceCss(byte[] Id, byte[] FamilyBytes, FontDisplay Display, GenericFontFamily Fallback, byte[][] ThemeVariables, FontMetrics? Metrics, ResourceCss[] Resources);
}
