// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text;
using System.Text.Json;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Renders a corpus with a reference engine through one adapter process.</summary>
internal static class ReferenceEngineRunner
{
    /// <summary>Engine name of MkDocs.</summary>
    internal const string MkDocs = "mkdocs";

    /// <summary>Engine name of Zensical.</summary>
    internal const string Zensical = "zensical";

    /// <summary>Request property that names the engine.</summary>
    private const string EngineProperty = "engine";

    /// <summary>Request property that holds the fragments.</summary>
    private const string FragmentsProperty = "fragments";

    /// <summary>Response property that holds the per-fragment results.</summary>
    private const string ResultsProperty = "results";

    /// <summary>Longest time one engine may take to render the whole corpus.</summary>
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Renders every fragment with the engine in a single adapter process.</summary>
    /// <param name="venvInterpreter">Interpreter of the reference virtual environment.</param>
    /// <param name="adapterPath">Path of the adapter script.</param>
    /// <param name="engine">Engine name, <see cref="MkDocs"/> or <see cref="Zensical"/>.</param>
    /// <param name="fragments">Fragments to render.</param>
    /// <returns>The engine's results.</returns>
    /// <exception cref="InvalidOperationException">The adapter failed; the message carries its error output.</exception>
    internal static async Task<ReferenceEngine> RunAsync(string venvInterpreter, string adapterPath, string engine, Fragment[] fragments)
    {
        var request = BuildRequest(engine, fragments);
        var result = await ProcessRunner.RunAsync(venvInterpreter, [adapterPath], request, RenderTimeout).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"The {engine} reference adapter failed (exit code {result.ExitCode}).\n{result.CombinedOutput}");
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Dictionary<string, RenderResult> results = [with(fragments.Length, StringComparer.Ordinal)];
        foreach (var entry in root.GetProperty(ResultsProperty).EnumerateObject())
        {
            results[entry.Name] = entry.Value.TryGetProperty("html", out var html)
                ? new(html.GetString(), null)
                : new(null, entry.Value.GetProperty("error").GetString());
        }

        return new(engine, root.GetProperty("version").GetString()!, null, results);
    }

    /// <summary>Builds the JSON request the adapter reads from standard input.</summary>
    /// <param name="engine">Engine name.</param>
    /// <param name="fragments">Fragments to render.</param>
    /// <returns>The request JSON.</returns>
    private static string BuildRequest(string engine, Fragment[] fragments)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(EngineProperty, engine);
            writer.WriteStartArray(FragmentsProperty);
            for (var i = 0; i < fragments.Length; i++)
            {
                writer.WriteStartObject();
                writer.WriteString("id", fragments[i].Id);
                writer.WriteString("markdown", fragments[i].Markdown);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }
}
