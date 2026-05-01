// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;

namespace NuStreamDocs.Benchmarks;

/// <summary>Benchmark host entry point.</summary>
public static class Program
{
    /// <summary>Runs the BenchmarkDotNet switcher against this assembly.</summary>
    /// <param name="args">Switcher command-line arguments.</param>
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
