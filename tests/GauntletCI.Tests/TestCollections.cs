// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Tests;

/// <summary>
/// Serial collection for tests that mutate process-wide Console.Out.
/// Both CliOutputTests and CorpusAutoLabelTests use Console.SetOut; running them
/// concurrently causes cross-test output capture races.
/// </summary>
[CollectionDefinition("ConsoleOut", DisableParallelization = true)]
public class ConsoleOutCollection { }

/// <summary>
/// Serial collection for tests that read/write ~/.gauntletci/config.json via TelemetryConsent.
/// End-to-end analyze tests also touch this file via InstallId; disable assembly parallelization
/// while any telemetry test is running to avoid Windows file-lock flakes on CI.
/// </summary>
[CollectionDefinition("TelemetrySerial", DisableParallelization = true)]
public class TelemetrySerialCollection { }
