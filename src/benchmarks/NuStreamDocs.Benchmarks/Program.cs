// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;
#if NET11_0_OR_GREATER
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Toolchains.Results;
#endif

namespace NuStreamDocs.Benchmarks;

/// <summary>Benchmark host entry point.</summary>
public static class Program
{
    /// <summary>Runs the BenchmarkDotNet switcher against this assembly.</summary>
    /// <param name="args">Switcher command-line arguments.</param>
    public static void Main(string[] args)
    {
#if NET11_0_OR_GREATER
        if (Array.Exists(args, static argument => argument is "-i" or "--inProcess" or "--cli" or "--coreRun"))
        {
            _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            return;
        }

        var settings = new NetCoreAppSettings("net11.0", null, ".NET 11.0");
        var components = CsProjCoreToolchain.From(settings);

        // Building the generated net11.0 project validates SDK support without an enum-based runtime lookup.
        var toolchain = new Toolchain(".NET SDK", new RuntimeProjectGenerator(), components.Builder, components.Executor);
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithOption(ConfigOptions.DisableParallelBuild, true)
            .AddJob(Job.Default.WithToolchain(toolchain).AsMutator());
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
#else
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
#endif
    }

#if NET11_0_OR_GREATER
    /// <summary>Generates benchmark projects for the runtime selected by each job.</summary>
    [System.Diagnostics.DebuggerDisplay("RuntimeProjectGenerator")]
    private sealed class RuntimeProjectGenerator : IGenerator
    {
        /// <inheritdoc/>
        public GenerateResult GenerateProject(BuildPartition buildPartition, BenchmarkDotNet.Loggers.ILogger logger, string rootArtifactsFolderPath)
        {
            var runtime = buildPartition.Runtime;
            var settings = new NetCoreAppSettings(runtime.MsBuildMoniker, null, runtime.Name);
            return CsProjCoreToolchain.From(settings).Generator.GenerateProject(buildPartition, logger, rootArtifactsFolderPath);
        }
    }
#endif
}
