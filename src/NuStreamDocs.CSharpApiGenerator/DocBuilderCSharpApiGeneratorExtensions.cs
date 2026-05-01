// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>Builder extension that registers the C# reference plugin.</summary>
/// <remarks>
/// Two flavours: <c>UseCSharpApiGenerator</c> respects the
/// <see cref="CSharpApiGeneratorOptions.Mode"/> field on the supplied
/// options (defaults to <see cref="CSharpApiGeneratorMode.EmitMarkdown"/>);
/// <c>UseCSharpApiGeneratorDirect</c> is a sugar overload that flips the
/// mode to <see cref="CSharpApiGeneratorMode.Direct"/> regardless of
/// what the supplied options say.
/// </remarks>
public static class DocBuilderCSharpApiGeneratorExtensions
{
    /// <summary>Registers <see cref="CSharpApiGeneratorPlugin"/> with the supplied options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Generator options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseCSharpApiGenerator(this DocBuilder builder, CSharpApiGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new CSharpApiGeneratorPlugin(options));
    }

    /// <summary>Registers <see cref="CSharpApiGeneratorPlugin"/> with options and a logger.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Generator options.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseCSharpApiGenerator(this DocBuilder builder, CSharpApiGeneratorOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new CSharpApiGeneratorPlugin(options, logger));
    }

    /// <summary>Registers <see cref="CSharpApiGeneratorPlugin"/> in <see cref="CSharpApiGeneratorMode.Direct"/> regardless of the supplied options' mode.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Generator options; the mode field is overridden to <see cref="CSharpApiGeneratorMode.Direct"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseCSharpApiGeneratorDirect(this DocBuilder builder, CSharpApiGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        var direct = options with { Mode = CSharpApiGeneratorMode.Direct };
        return builder.UsePlugin(new CSharpApiGeneratorPlugin(direct));
    }
}
