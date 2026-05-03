// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;

namespace GauntletCI.Cli.Commands.Factories;

/// <summary>
/// Base interface for command builders. Enables dependency injection and factory pattern registration.
/// Each factory implementation builds related commands (e.g., CorpusOperationsFactory builds CRUD commands).
/// </summary>
public interface ICommandFactory
{
}

/// <summary>
/// Factory for basic CorpusCommand operations: add-pr, normalize, list, show, status, batch-hydrate.
/// </summary>
public interface ICorpusOperationsFactory : ICommandFactory
{
    Command CreateAddPr();
    Command CreateNormalize();
    Command CreateList();
    Command CreateShow();
    Command CreateStatus();
    Command CreateBatchHydrate();
}

/// <summary>
/// Factory for corpus analysis commands: discover, run, run-all, score, report.
/// </summary>
public interface ICorpusAnalysisFactory : ICommandFactory
{
    Command CreateDiscover();
    Command CreateRun();
    Command CreateRunAll();
    Command CreateScore();
    Command CreateReport();
}

/// <summary>
/// Factory for corpus labeling commands: label, label-all, reset-stats.
/// </summary>
public interface ICorpusLabelingFactory : ICommandFactory
{
    Command CreateLabel();
    Command CreateLabelAll();
    Command CreateResetStats();
}

/// <summary>
/// Factory for corpus utility commands: purge, errors, rejected-repos, doctor.
/// </summary>
public interface ICorpusUtilityFactory : ICommandFactory
{
    Command CreatePurge();
    Command CreateErrors();
    Command CreateRejectedRepos();
    Command CreateDoctor();
}

/// <summary>
/// Concrete wrapper implementation for ICorpusOperationsFactory that delegates to static CorpusOperationsFactory methods.
/// Enables dependency injection while preserving static method organization for zero-state command builders.
/// </summary>
internal class CorpusOperationsFactoryImpl : ICorpusOperationsFactory
{
    public Command CreateAddPr() => CorpusOperationsFactory.CreateAddPr();
    public Command CreateNormalize() => CorpusOperationsFactory.CreateNormalize();
    public Command CreateList() => CorpusOperationsFactory.CreateList();
    public Command CreateShow() => CorpusOperationsFactory.CreateShow();
    public Command CreateStatus() => CorpusOperationsFactory.CreateStatus();
    public Command CreateBatchHydrate() => CorpusOperationsFactory.CreateBatchHydrate();
}

/// <summary>
/// Concrete wrapper implementation for ICorpusAnalysisFactory that delegates to static CorpusAnalysisFactory methods.
/// </summary>
internal class CorpusAnalysisFactoryImpl : ICorpusAnalysisFactory
{
    public Command CreateDiscover() => CorpusAnalysisFactory.CreateDiscover();
    public Command CreateRun() => CorpusAnalysisFactory.CreateRun();
    public Command CreateRunAll() => CorpusAnalysisFactory.CreateRunAll();
    public Command CreateScore() => CorpusAnalysisFactory.CreateScore();
    public Command CreateReport() => CorpusAnalysisFactory.CreateReport();
}

/// <summary>
/// Concrete wrapper implementation for ICorpusLabelingFactory that delegates to static CorpusLabelingFactory methods.
/// </summary>
internal class CorpusLabelingFactoryImpl : ICorpusLabelingFactory
{
    public Command CreateLabel() => CorpusLabelingFactory.CreateLabel();
    public Command CreateLabelAll() => CorpusLabelingFactory.CreateLabelAll();
    public Command CreateResetStats() => CorpusLabelingFactory.CreateResetStats();
}

/// <summary>
/// Concrete wrapper implementation for ICorpusUtilityFactory that delegates to static CorpusUtilityFactory methods.
/// </summary>
internal class CorpusUtilityFactoryImpl : ICorpusUtilityFactory
{
    public Command CreatePurge() => CorpusUtilityFactory.CreatePurge();
    public Command CreateErrors() => CorpusUtilityFactory.CreateErrors();
    public Command CreateRejectedRepos() => CorpusUtilityFactory.CreateRejectedRepos();
    public Command CreateDoctor() => CorpusUtilityFactory.CreateDoctor();
}
