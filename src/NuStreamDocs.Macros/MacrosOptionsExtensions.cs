// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Macros;

/// <summary>Construction helpers for the byte-shaped <see cref="MacrosOptions"/>.</summary>
/// <remarks>
/// The <see cref="byte"/>-shaped overloads are the canonical hot path — callers that already hold
/// UTF-8 bytes use them directly. The <see cref="string"/> overloads encode once at construction
/// for callers driven by YAML/TOML config readers that produce strings.
/// </remarks>
public static class MacrosOptionsExtensions
{
    /// <summary>Returns a copy of <paramref name="options"/> with one extra UTF-8 <c>(name, value)</c> pair added to the variables map.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="name">UTF-8 variable name bytes.</param>
    /// <param name="value">UTF-8 variable value bytes.</param>
    /// <returns>The updated options.</returns>
    public static MacrosOptions WithVariable(this MacrosOptions options, byte[] name, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        if (name.Length is 0)
        {
            throw new ArgumentException("Name must be non-empty.", nameof(name));
        }

        return WithVariableCore(options, name, value);
    }

    /// <summary>String adapter for <see cref="WithVariable(MacrosOptions, byte[], byte[])"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    /// <returns>The updated options.</returns>
    /// <remarks>Encodes both inputs to UTF-8 once and delegates to the byte overload.</remarks>
    public static MacrosOptions WithVariable(this MacrosOptions options, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        return WithVariableCore(options, Encoding.UTF8.GetBytes(name), Encoding.UTF8.GetBytes(value));
    }

    /// <summary>Returns a copy of <paramref name="options"/> with a fresh UTF-8 variables map seeded from <paramref name="variables"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="variables">UTF-8 byte-keyed variable map.</param>
    /// <returns>The updated options.</returns>
    public static MacrosOptions WithVariables(this MacrosOptions options, Dictionary<byte[], byte[]> variables)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(variables);
        var map = new Dictionary<byte[], byte[]>(variables.Count, ByteArrayComparer.Instance);
        foreach (var pair in variables)
        {
            ArgumentNullException.ThrowIfNull(pair.Key);
            ArgumentNullException.ThrowIfNull(pair.Value);
            map[pair.Key] = pair.Value;
        }

        return options with { Variables = map };
    }

    /// <summary>String adapter for <see cref="WithVariables(MacrosOptions, Dictionary{byte[], byte[]})"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="variables">String → string variable map; both name and value are encoded once to UTF-8.</param>
    /// <returns>The updated options.</returns>
    public static MacrosOptions WithVariables(this MacrosOptions options, Dictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(variables);
        var map = new Dictionary<byte[], byte[]>(variables.Count, ByteArrayComparer.Instance);
        foreach (var pair in variables)
        {
            ArgumentException.ThrowIfNullOrEmpty(pair.Key);
            ArgumentNullException.ThrowIfNull(pair.Value);
            map[Encoding.UTF8.GetBytes(pair.Key)] = Encoding.UTF8.GetBytes(pair.Value);
        }

        return options with { Variables = map };
    }

    /// <summary>Builds a new variables dictionary by copying every existing entry and overlaying <paramref name="name"/> / <paramref name="value"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="name">UTF-8 name bytes.</param>
    /// <param name="value">UTF-8 value bytes.</param>
    /// <returns>The updated options.</returns>
    private static MacrosOptions WithVariableCore(MacrosOptions options, byte[] name, byte[] value)
    {
        var map = new Dictionary<byte[], byte[]>(options.Variables.Count + 1, ByteArrayComparer.Instance);
        foreach (var pair in options.Variables)
        {
            map[pair.Key] = pair.Value;
        }

        map[name] = value;
        return options with { Variables = map };
    }
}
