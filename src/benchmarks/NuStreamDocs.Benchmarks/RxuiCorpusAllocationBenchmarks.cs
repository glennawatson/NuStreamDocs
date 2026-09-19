// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace NuStreamDocs.Benchmarks;

/// <summary>Profiles corpus allocations separately from build timing measurements.</summary>
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class RxuiCorpusAllocationBenchmarks : RxuiCorpusBenchmarks;
