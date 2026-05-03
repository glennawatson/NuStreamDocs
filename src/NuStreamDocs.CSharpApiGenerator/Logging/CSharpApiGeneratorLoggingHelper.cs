// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.CSharpApiGenerator.Logging;

/// <summary>
/// Source-generated logging entry points for <see cref="CSharpApiGenerator"/>
/// and <see cref="CSharpApiGeneratorPlugin"/>.
/// </summary>
/// <remarks>
/// All methods are <see cref="LoggerMessageAttribute"/> partials so the
/// generator emits the underlying <c>EventId</c> + cached delegate. The
/// per-assembly and source-link-miss hooks are reserved for future
/// pipeline points where SourceDocParser surfaces those events to the
/// host plugin.
/// </remarks>
internal static partial class CSharpApiGeneratorLoggingHelper
{
    /// <summary>Logs the start of a generator run.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="rootDirectory">Repository root holding the package config.</param>
    /// <param name="outputRoot">Destination directory for the emitted Markdown.</param>
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "ApiGenerator starting: root={RootDirectory}, output={OutputRoot}")]
    public static partial void LogGeneratorStart(ILogger logger, string rootDirectory, string outputRoot);

    /// <summary>Logs the completion of a per-assembly walk.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="assemblyName">Walked assembly name.</param>
    /// <param name="typeCount">Types extracted from the assembly.</param>
    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "ApiGenerator walked {AssemblyName}: {TypeCount} type(s)")]
    public static partial void LogAssemblyWalked(ILogger logger, string assemblyName, int typeCount);

    /// <summary>Logs the end-of-run summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="totalTypes">Total canonical types processed.</param>
    /// <param name="totalPages">Total Markdown pages emitted.</param>
    /// <param name="elapsedSeconds">Wall-clock duration in seconds (two decimal places).</param>
    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "ApiGenerator complete: {TotalTypes} type(s), {TotalPages} page(s) in {ElapsedSeconds:F3}s")]
    public static partial void LogGeneratorComplete(ILogger logger, int totalTypes, int totalPages, double elapsedSeconds);

    /// <summary>Logs a source-link miss at debug level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="assemblyName">Owning assembly.</param>
    /// <param name="typeFullName">Type that lacks a source-link mapping.</param>
    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Debug,
        Message = "CSharpApiGenerator source-link miss in {AssemblyName} for {TypeFullName}")]
    public static partial void LogSourceLinkMiss(ILogger logger, string assemblyName, string typeFullName);

    /// <summary>Logs the start of a direct-mode extract run.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="rootDirectory">Repository root holding the package config.</param>
    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Information,
        Message = "CSharpApiGenerator direct-extract starting: root={RootDirectory}")]
    public static partial void LogDirectExtractStart(ILogger logger, string rootDirectory);

    /// <summary>Logs the completion of a direct-mode extract run.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="typeCount">Canonical types in the merged catalog.</param>
    /// <param name="sourceLinkCount">Source-link entries collected.</param>
    /// <param name="elapsedSeconds">Wall-clock duration in seconds (two decimal places).</param>
    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Information,
        Message = "CSharpApiGenerator direct-extract complete: {TypeCount} type(s), {SourceLinkCount} source link(s) in {ElapsedSeconds:F3}s")]
    public static partial void LogDirectExtractComplete(ILogger logger, int typeCount, int sourceLinkCount, double elapsedSeconds);
}
