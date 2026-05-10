// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Arithmatex.MathJax;

/// <summary>Builder-extension surface for <see cref="MathJaxPlugin"/>.</summary>
public static class DocBuilderMathJaxExtensions
{
    /// <summary>Registers <see cref="MathJaxPlugin"/> with default options (CDN-loaded).</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMathJax(this DocBuilder builder)
    {
        return builder.UsePlugin(new MathJaxPlugin());
    }

    /// <summary>Registers <see cref="MathJaxPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="MathJaxOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMathJax(this DocBuilder builder, Func<MathJaxOptions, MathJaxOptions> configure)
    {
        var options = configure(MathJaxOptions.Default);
        return builder.UsePlugin(new MathJaxPlugin(options));
    }

    /// <summary>Registers <see cref="MathJaxPlugin"/> with caller-supplied options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMathJax(this DocBuilder builder, in MathJaxOptions options)
    {
        return builder.UsePlugin(new MathJaxPlugin(options));
    }
}
