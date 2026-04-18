// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Tests;

/// <summary>
/// Serial collection for tests that mutate process-wide Console.Out.
/// Both CliOutputTests and CorpusAutoLabelTests use Console.SetOut; running them
/// concurrently causes cross-test output capture races.
/// </summary>
[CollectionDefinition("ConsoleOut", DisableParallelization = true)]
public class ConsoleOutCollection { }
