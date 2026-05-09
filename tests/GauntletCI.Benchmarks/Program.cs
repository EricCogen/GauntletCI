// SPDX-License-Identifier: Elastic-2.0
using BenchmarkDotNet.Running;
using GauntletCI.Benchmarks;

var summary = BenchmarkRunner.Run<PerformanceBenchmarks>();
