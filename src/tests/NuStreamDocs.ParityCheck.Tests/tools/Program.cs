#!/usr/bin/env -S dotnet --
// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#:project ../../../NuStreamDocs/NuStreamDocs.csproj
#:include ../Parity/*.cs
#:include CSharpLiteral.cs
#:include EnginePair.cs
#:include ParityOptions.cs
#:include ParityReport.cs
#:include PinnedRowEmitter.cs
#:include PinnedRowSource.cs
#:include SidecarPinner.cs
#:include TestRowEmitter.cs
#:property PublishAot=false

using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Maintenance tool for the parity corpus: lists fragments, shows differences, pins sidecars and emits test rows.</summary>
public static class Program
{
    /// <summary>Exit code for a usage error.</summary>
    private const int UsageError = 2;

    /// <summary>Runs the tool.</summary>
    /// <param name="args">Command-line arguments; see the project README.</param>
    /// <returns>0 on success, 1 when a fragment fails or the reference environment cannot be prepared, 2 for a usage error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<int> Main(string[] args) => RunAsync(args, ProjectDirectory());

    /// <summary>Gets the directory of the parity test project, derived from the location of this source file.</summary>
    /// <param name="sourceFile">Path of this source file, supplied by the compiler.</param>
    /// <returns>The project directory.</returns>
    private static string ProjectDirectory([CallerFilePath] string sourceFile = "") => Path.GetDirectoryName(Path.GetDirectoryName(sourceFile))!;

    /// <summary>Runs the tool against a project directory.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="projectDirectory">Directory of the parity test project.</param>
    /// <returns>The process exit code.</returns>
    private static async Task<int> RunAsync(string[] args, string projectDirectory)
    {
        var options = ParityOptions.Parse(args, Path.Combine(projectDirectory, "fragments"));
        if (options.Error is not null)
        {
            await Console.Error.WriteLineAsync(options.Error);
            return UsageError;
        }

        var corpus = FragmentCorpus.Load(options.FragmentsDirectory);
        if (options.EmitPinned)
        {
            return await WritePinnedTestsAsync(corpus, projectDirectory);
        }

        var fragments = Select(options, corpus);
        if (options.List)
        {
            List(fragments);
            return 0;
        }

        if (fragments.Length is 0)
        {
            await Console.Error.WriteLineAsync("No fragments matched.");
            return UsageError;
        }

        if (options.PinKind is not null)
        {
            return Pin(options, fragments);
        }

        try
        {
            return await CompareAsync(options, projectDirectory, fragments);
        }
        catch (InvalidOperationException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    /// <summary>Selects the fragments that pass the command-line filters.</summary>
    /// <param name="options">Parsed command line.</param>
    /// <param name="corpus">The whole corpus.</param>
    /// <returns>The selected fragments.</returns>
    private static Fragment[] Select(ParityOptions options, Fragment[] corpus)
    {
        var selected = new List<Fragment>(corpus.Length);
        foreach (var fragment in corpus)
        {
            if (options.Selects(fragment))
            {
                selected.Add(fragment);
            }
        }

        return [.. selected];
    }

    /// <summary>Prints the fragment ids with their sidecar kinds.</summary>
    /// <param name="fragments">Fragments to list.</param>
    private static void List(Fragment[] fragments)
    {
        var output = Console.Out;
        foreach (var fragment in fragments)
        {
            output.WriteLine(fragment.Expect is { } expect ? $"{fragment.Id}  [{expect.Kind}] {expect.Reason}" : fragment.Id);
        }

        output.WriteLine($"{fragments.Length} fragments");
    }

    /// <summary>Regenerates the pinned-output tests from the sidecars.</summary>
    /// <param name="corpus">The whole corpus.</param>
    /// <param name="projectDirectory">Directory of the parity test project.</param>
    /// <returns>The process exit code.</returns>
    private static async Task<int> WritePinnedTestsAsync(Fragment[] corpus, string projectDirectory)
    {
        var source = PinnedRowEmitter.Emit(corpus);
        var target = Path.Combine(projectDirectory, PinnedOutputNames.FileName);
        await File.WriteAllTextAsync(target, source.Text, new UTF8Encoding(false));
        await Console.Out.WriteLineAsync($"wrote {source.RowCount} rows to {target}");
        return 0;
    }

    /// <summary>Writes sidecars for the selected fragments.</summary>
    /// <param name="options">Parsed command line.</param>
    /// <param name="fragments">Selected fragments.</param>
    /// <returns>The process exit code.</returns>
    private static int Pin(ParityOptions options, Fragment[] fragments)
    {
        if (options.Filters.Count is 0)
        {
            Console.Error.WriteLine("--pin needs at least one --id or --filter.");
            return UsageError;
        }

        if (Array.IndexOf(ExpectationKinds.All, options.PinKind) < 0)
        {
            Console.Error.WriteLine($"Unknown sidecar kind '{options.PinKind}'; use one of: {string.Join(", ", ExpectationKinds.All)}.");
            return UsageError;
        }

        SidecarPinner.Pin(Console.Out, options.FragmentsDirectory, fragments, options.PinKind!, options.PinReason!);
        return 0;
    }

    /// <summary>Renders the fragments with all engines and prints the comparison.</summary>
    /// <param name="options">Parsed command line.</param>
    /// <param name="projectDirectory">Directory of the parity test project.</param>
    /// <param name="fragments">Selected fragments.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="InvalidOperationException">The reference environment could not be prepared or an engine failed.</exception>
    private static async Task<int> CompareAsync(ParityOptions options, string projectDirectory, Fragment[] fragments)
    {
        var probe = await PythonProbe.FindAsync();
        if (probe.Interpreter is null)
        {
            await Console.Error.WriteLineAsync(probe.DescribeMissing());
            return 1;
        }

        var venvInterpreter = await ReferenceVenv.EnsureAsync(probe.Interpreter, ReferenceVenvLocation.Resolve(projectDirectory));
        var adapter = Path.Combine(projectDirectory, "reference_adapter.py");
        var mkdocsTask = ReferenceEngineRunner.RunAsync(venvInterpreter, adapter, ReferenceEngineRunner.MkDocs, fragments);
        var zensicalTask = options.UseZensical
            ? ReferenceEngineRunner.RunAsync(venvInterpreter, adapter, ReferenceEngineRunner.Zensical, fragments)
            : Task.FromResult(ReferenceEngine.Disabled(ReferenceEngineRunner.Zensical, "disabled by --no-zensical"));
        EnginePair engines = new(await mkdocsTask, await zensicalTask);

        var outcomes = new List<ParityOutcome>(fragments.Length);
        foreach (var fragment in fragments)
        {
            outcomes.Add(ParityClassifier.Evaluate(fragment, OursRenderer.Render(fragment.Markdown), engines.MkDocs, engines.Zensical));
        }

        return options.EmitFeature is not null ? EmitRows(options, engines, outcomes) : Report(options, engines, outcomes);
    }

    /// <summary>Prints the report as text or JSON.</summary>
    /// <param name="options">Parsed command line.</param>
    /// <param name="engines">Both engines.</param>
    /// <param name="outcomes">Outcomes of the selected fragments.</param>
    /// <returns>1 when any fragment fails, otherwise 0.</returns>
    private static int Report(ParityOptions options, EnginePair engines, List<ParityOutcome> outcomes)
    {
        if (options.Json)
        {
            using var stdout = Console.OpenStandardOutput();
            ParityReport.WriteJson(stdout, engines, outcomes);
            Console.Out.WriteLine();
            return outcomes.Exists(static outcome => outcome.Fails) ? 1 : 0;
        }

        return ParityReport.PrintAll(Console.Out, engines, outcomes, options.ShowAll) is 0 ? 0 : 1;
    }

    /// <summary>Prints TUnit <c>[Arguments]</c> rows for the selected fragments.</summary>
    /// <param name="options">Parsed command line.</param>
    /// <param name="engines">Both engines.</param>
    /// <param name="outcomes">Outcomes of the selected fragments.</param>
    /// <returns>The process exit code.</returns>
    private static int EmitRows(ParityOptions options, EnginePair engines, List<ParityOutcome> outcomes)
    {
        var preferZensical = options.EmitReference.Equals(ReferenceEngineRunner.Zensical, StringComparison.OrdinalIgnoreCase);
        if (!preferZensical && !options.EmitReference.Equals(ReferenceEngineRunner.MkDocs, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown reference '{options.EmitReference}'; use zensical or mkdocs.");
            return UsageError;
        }

        var reference = preferZensical ? engines.Zensical : engines.MkDocs;
        if (!reference.Available)
        {
            Console.Error.WriteLine($"Reference {reference.Name} is {reference.Label}.");
            return UsageError;
        }

        var output = Console.Out;
        output.WriteLine($"// {reference.Label}; expected HTML is our output where it matches the reference, else the reference output (mkdocs-better and extension fragments use MkDocs).");
        foreach (var outcome in outcomes)
        {
            output.WriteLine(TestRowEmitter.EmitRow(outcome, preferZensical));
        }

        return 0;
    }
}
