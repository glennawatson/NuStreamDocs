// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Macros;

namespace NuStreamDocs.Macros.Tests;

/// <summary>Tests for <see cref="MacrosScanner"/> — variable substitution + code-region passthrough.</summary>
public class MacrosScannerTests
{
    /// <summary>Simple <c>{{ name }}</c> resolves to the dictionary value.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SimpleVariableExpands()
    {
        var output = Rewrite("Hello {{ name }}!", new() { ["name"] = "world" });
        await Assert.That(output).IsEqualTo("Hello world!");
    }

    /// <summary>Whitespace inside the braces is optional.</summary>
    /// <param name="input">Input source.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("Hi {{name}}")]
    [Arguments("Hi {{ name }}")]
    [Arguments("Hi {{   name   }}")]
    public async Task WhitespaceVariantsAllResolve(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var output = Rewrite(input, new() { ["name"] = "x" });
        await Assert.That(output).IsEqualTo("Hi x");
    }

    /// <summary>An unknown name is left in place verbatim when no missing-callback is set.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownVariableLeftInPlace()
    {
        var output = Rewrite("Hello {{ unknown }}!", new() { ["other"] = "x" });
        await Assert.That(output).IsEqualTo("Hello {{ unknown }}!");
    }

    /// <summary>Fenced code blocks are not touched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeIsSkipped()
    {
        const string input = """
            Hello {{ name }}.

            ```python
            print("{{ name }}")
            ```

            And {{ name }} again.
            """;
        var output = Rewrite(input, new() { ["name"] = "x" });
        await Assert.That(output).Contains("Hello x.");
        await Assert.That(output).Contains("print(\"{{ name }}\")");
        await Assert.That(output).Contains("And x again.");
    }

    /// <summary>Inline code spans (<c>`...`</c>) are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodeIsSkipped()
    {
        var output = Rewrite("Use `{{ name }}` to interpolate {{ name }}.", new() { ["name"] = "world" });
        await Assert.That(output).IsEqualTo("Use `{{ name }}` to interpolate world.");
    }

    /// <summary>Truncated <c>{{</c> with no closing <c>}}</c> passes through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnterminatedMarkerPassesThrough()
    {
        var output = Rewrite("Hello {{ name", new() { ["name"] = "x" });
        await Assert.That(output).IsEqualTo("Hello {{ name");
    }

    /// <summary>Names with non-identifier characters (spaces inside, dollar signs, etc.) are not substituted.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InvalidNameLeftInPlace()
    {
        var output = Rewrite("{{ has space }}", new() { ["has space"] = "x" });
        await Assert.That(output).IsEqualTo("{{ has space }}");
    }

    /// <summary>Dotted, hyphenated, and underscored names are valid.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DottedAndHyphenatedNamesResolve()
    {
        var vars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site.name"] = "S",
            ["my-var"] = "H",
            ["snake_case"] = "K",
        };
        var output = Rewrite("{{ site.name }} / {{ my-var }} / {{ snake_case }}", vars);
        await Assert.That(output).IsEqualTo("S / H / K");
    }

    /// <summary>EscapeHtml on emits HTML-escaped values.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EscapeHtmlEscapesEntities()
    {
        var output = RewriteEscaped("Value: {{ x }}", new() { ["x"] = "<a&b>" });
        await Assert.That(output).IsEqualTo("Value: &lt;a&amp;b&gt;");
    }

    /// <summary>Multiple substitutions in one pass.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleSubstitutionsInOnePass()
    {
        var output = Rewrite("{{ a }} - {{ b }} - {{ a }}", new() { ["a"] = "X", ["b"] = "Y" });
        await Assert.That(output).IsEqualTo("X - Y - X");
    }

    /// <summary>Plugin entry point with no variables registers and is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PluginNoVariablesIsNoOp()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        const string input = "Hello {{ name }}.";
        new MacrosPlugin().Preprocess(Encoding.UTF8.GetBytes(input), sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(input);
    }

    /// <summary>WarnOnMissing fires the missing callback (verified via the public delegate path).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingCallbackFires()
    {
        var missing = new List<string>();
        var sink = new ArrayBufferWriter<byte>(64);
        MacrosScanner.Rewrite(
            "Hi {{ name }}, {{ other }}"u8,
            (ReadOnlySpan<byte> n, out string v) =>
            {
                if (n.SequenceEqual("name"u8))
                {
                    v = "world";
                    return true;
                }

                v = string.Empty;
                return false;
            },
            escapeHtml: false,
            onMissing: name => missing.Add(Encoding.UTF8.GetString(name)),
            sink);

        await Assert.That(missing.Count).IsEqualTo(1);
        await Assert.That(missing[0]).IsEqualTo("other");
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("Hi world, {{ other }}");
    }

    /// <summary>Helper that runs the plugin and returns the result string.</summary>
    /// <param name="input">Markdown source.</param>
    /// <param name="variables">Variables dictionary.</param>
    /// <returns>Rewritten markdown.</returns>
    private static string Rewrite(string input, Dictionary<string, string> variables)
    {
        var plugin = new MacrosPlugin(MacrosOptions.Default with { Variables = variables });
        var sink = new ArrayBufferWriter<byte>(input.Length * 2);
        plugin.Preprocess(Encoding.UTF8.GetBytes(input), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }

    /// <summary>Helper that runs the plugin with HTML escaping and returns the result string.</summary>
    /// <param name="input">Markdown source.</param>
    /// <param name="variables">Variables dictionary.</param>
    /// <returns>Rewritten markdown.</returns>
    private static string RewriteEscaped(string input, Dictionary<string, string> variables)
    {
        var plugin = new MacrosPlugin(MacrosOptions.Default with { Variables = variables, EscapeHtml = true });
        var sink = new ArrayBufferWriter<byte>(input.Length * 2);
        plugin.Preprocess(Encoding.UTF8.GetBytes(input), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
