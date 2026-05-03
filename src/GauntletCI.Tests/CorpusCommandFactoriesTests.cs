// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Commands;
using GauntletCI.Cli.Commands.Factories;

namespace GauntletCI.Tests;

public class CorpusOperationsFactoryTests
{
    [Fact]
    public void CreateAddPr_ReturnsValidCommand()
    {
        var cmd = CorpusOperationsFactory.CreateAddPr();

        Assert.NotNull(cmd);
        Assert.Equal("add-pr", cmd.Name);
        Assert.NotNull(cmd.Description);
        Assert.NotEmpty(cmd.Description);
    }

    [Fact]
    public void CreateAddPr_HasRequiredUrlOption()
    {
        var cmd = CorpusOperationsFactory.CreateAddPr();
        var urlOpt = cmd.Options.FirstOrDefault(o => o.Name == "url");

        Assert.NotNull(urlOpt);
        Assert.True(urlOpt.IsRequired);
    }

    [Fact]
    public void CreateAddPr_HasDefaultDbOption()
    {
        var cmd = CorpusOperationsFactory.CreateAddPr();
        var dbOpt = cmd.Options.FirstOrDefault(o => o.Name == "db");

        Assert.NotNull(dbOpt);
        Assert.False(dbOpt.IsRequired);
    }

    [Fact]
    public void CreateNormalize_ReturnsValidCommand()
    {
        var cmd = CorpusOperationsFactory.CreateNormalize();

        Assert.NotNull(cmd);
        Assert.Equal("normalize", cmd.Name);
        Assert.NotNull(cmd.Description);
        Assert.NotEmpty(cmd.Description);
    }

    [Fact]
    public void CreateList_ReturnsValidCommand()
    {
        var cmd = CorpusOperationsFactory.CreateList();

        Assert.NotNull(cmd);
        Assert.Equal("list", cmd.Name);
    }

    [Fact]
    public void CreateShow_ReturnsValidCommand()
    {
        var cmd = CorpusOperationsFactory.CreateShow();

        Assert.NotNull(cmd);
        Assert.Equal("show", cmd.Name);
    }

    [Fact]
    public void CreateStatus_ReturnsValidCommand()
    {
        var cmd = CorpusOperationsFactory.CreateStatus();

        Assert.NotNull(cmd);
        Assert.Equal("status", cmd.Name);
    }

    [Fact]
    public void CreateBatchHydrate_ReturnsValidCommand()
    {
        var cmd = CorpusOperationsFactory.CreateBatchHydrate();

        Assert.NotNull(cmd);
        Assert.Equal("batch-hydrate", cmd.Name);
    }
}

public class CorpusAnalysisFactoryTests
{
    [Fact]
    public void CreateDiscover_ReturnsValidCommand()
    {
        var cmd = CorpusAnalysisFactory.CreateDiscover();

        Assert.NotNull(cmd);
        Assert.Equal("discover", cmd.Name);
        Assert.NotNull(cmd.Description);
        Assert.NotEmpty(cmd.Description);
    }

    [Fact]
    public void CreateDiscover_HasProviderOption()
    {
        var cmd = CorpusAnalysisFactory.CreateDiscover();
        var providerOpt = cmd.Options.FirstOrDefault(o => o.Name == "provider");

        Assert.NotNull(providerOpt);
    }

    [Fact]
    public void CreateRun_ReturnsValidCommand()
    {
        var cmd = CorpusAnalysisFactory.CreateRun();

        Assert.NotNull(cmd);
        Assert.Equal("run", cmd.Name);
        Assert.NotNull(cmd.Description);
        Assert.NotEmpty(cmd.Description);
    }

    [Fact]
    public void CreateRunAll_ReturnsValidCommand()
    {
        var cmd = CorpusAnalysisFactory.CreateRunAll();

        Assert.NotNull(cmd);
        Assert.Equal("run-all", cmd.Name);
    }

    [Fact]
    public void CreateScore_ReturnsValidCommand()
    {
        var cmd = CorpusAnalysisFactory.CreateScore();

        Assert.NotNull(cmd);
        Assert.Equal("score", cmd.Name);
    }

    [Fact]
    public void CreateReport_ReturnsValidCommand()
    {
        var cmd = CorpusAnalysisFactory.CreateReport();

        Assert.NotNull(cmd);
        Assert.Equal("report", cmd.Name);
    }
}

public class CorpusLabelingFactoryTests
{
    [Fact]
    public void CreateLabel_ReturnsValidCommand()
    {
        var cmd = CorpusLabelingFactory.CreateLabel();

        Assert.NotNull(cmd);
        Assert.Equal("label", cmd.Name);
        Assert.NotNull(cmd.Description);
        Assert.NotEmpty(cmd.Description);
    }

    [Fact]
    public void CreateLabel_HasFixtureOption()
    {
        var cmd = CorpusLabelingFactory.CreateLabel();
        var fixtureOpt = cmd.Options.FirstOrDefault(o => o.Name == "fixture");

        Assert.NotNull(fixtureOpt);
    }

    [Fact]
    public void CreateLabelAll_ReturnsValidCommand()
    {
        var cmd = CorpusLabelingFactory.CreateLabelAll();

        Assert.NotNull(cmd);
        Assert.Equal("label-all", cmd.Name);
    }

    [Fact]
    public void CreateResetStats_ReturnsValidCommand()
    {
        var cmd = CorpusLabelingFactory.CreateResetStats();

        Assert.NotNull(cmd);
        Assert.Equal("reset-stats", cmd.Name);
    }
}

public class CorpusUtilityFactoryTests
{
    [Fact]
    public void CreatePurge_ReturnsValidCommand()
    {
        var cmd = CorpusUtilityFactory.CreatePurge();

        Assert.NotNull(cmd);
        Assert.Equal("purge", cmd.Name);
        Assert.NotNull(cmd.Description);
        Assert.NotEmpty(cmd.Description);
    }

    [Fact]
    public void CreatePurge_HasLanguageOption()
    {
        var cmd = CorpusUtilityFactory.CreatePurge();
        var languageOpt = cmd.Options.FirstOrDefault(o => o.Name == "language");

        Assert.NotNull(languageOpt);
    }

    [Fact]
    public void CreateErrors_ReturnsValidCommand()
    {
        var cmd = CorpusUtilityFactory.CreateErrors();

        Assert.NotNull(cmd);
        Assert.Equal("errors", cmd.Name);
    }

    [Fact]
    public void CreateRejectedRepos_ReturnsValidCommand()
    {
        var cmd = CorpusUtilityFactory.CreateRejectedRepos();

        Assert.NotNull(cmd);
        Assert.Equal("rejected-repos", cmd.Name);
    }

    [Fact]
    public void CreateDoctor_ReturnsValidCommand()
    {
        var cmd = CorpusUtilityFactory.CreateDoctor();

        Assert.NotNull(cmd);
        Assert.Equal("doctor", cmd.Name);
        Assert.NotNull(cmd.Description);
        Assert.NotEmpty(cmd.Description);
    }
}

public class CommandFactoryDiTests
{
    [Fact]
    public void CorpusOperationsFactoryImpl_CanBeInstantiated()
    {
        var factory = new CorpusOperationsFactoryImpl();
        Assert.NotNull(factory);
    }

    [Fact]
    public void CorpusOperationsFactoryImpl_ImplementsInterface()
    {
        var factory = new CorpusOperationsFactoryImpl();
        Assert.IsAssignableFrom<ICorpusOperationsFactory>(factory);
    }

    [Fact]
    public void CorpusOperationsFactoryImpl_CreateAddPr_DelegatesProperly()
    {
        var factory = new CorpusOperationsFactoryImpl();
        var cmd1 = factory.CreateAddPr();
        var cmd2 = CorpusOperationsFactory.CreateAddPr();

        Assert.NotNull(cmd1);
        Assert.NotNull(cmd2);
        Assert.Equal(cmd1.Name, cmd2.Name);
    }

    [Fact]
    public void CorpusAnalysisFactoryImpl_CanBeInstantiated()
    {
        var factory = new CorpusAnalysisFactoryImpl();
        Assert.NotNull(factory);
    }

    [Fact]
    public void CorpusAnalysisFactoryImpl_ImplementsInterface()
    {
        var factory = new CorpusAnalysisFactoryImpl();
        Assert.IsAssignableFrom<ICorpusAnalysisFactory>(factory);
    }

    [Fact]
    public void CorpusLabelingFactoryImpl_CanBeInstantiated()
    {
        var factory = new CorpusLabelingFactoryImpl();
        Assert.NotNull(factory);
    }

    [Fact]
    public void CorpusLabelingFactoryImpl_ImplementsInterface()
    {
        var factory = new CorpusLabelingFactoryImpl();
        Assert.IsAssignableFrom<ICorpusLabelingFactory>(factory);
    }

    [Fact]
    public void CorpusUtilityFactoryImpl_CanBeInstantiated()
    {
        var factory = new CorpusUtilityFactoryImpl();
        Assert.NotNull(factory);
    }

    [Fact]
    public void CorpusUtilityFactoryImpl_ImplementsInterface()
    {
        var factory = new CorpusUtilityFactoryImpl();
        Assert.IsAssignableFrom<ICorpusUtilityFactory>(factory);
    }

    [Fact]
    public void AllFactoryImplsImplementICommandFactory()
    {
        var opsImpl = new CorpusOperationsFactoryImpl();
        var analysisImpl = new CorpusAnalysisFactoryImpl();
        var labelingImpl = new CorpusLabelingFactoryImpl();
        var utilityImpl = new CorpusUtilityFactoryImpl();

        Assert.IsAssignableFrom<ICommandFactory>(opsImpl);
        Assert.IsAssignableFrom<ICommandFactory>(analysisImpl);
        Assert.IsAssignableFrom<ICommandFactory>(labelingImpl);
        Assert.IsAssignableFrom<ICommandFactory>(utilityImpl);
    }
}

public class CorpusCommandIntegrationTests
{
    [Fact]
    public void CorpusCommand_Create_ReturnsValidCommand()
    {
        var cmd = CorpusCommand.Create();

        Assert.NotNull(cmd);
        Assert.Equal("corpus", cmd.Name);
        Assert.NotNull(cmd.Description);
        Assert.NotEmpty(cmd.Description);
    }

    [Fact]
    public void CorpusCommand_HasAllOperationsCommands()
    {
        var cmd = CorpusCommand.Create();
        var subcommands = cmd.Subcommands.Select(s => s.Name).ToList();

        Assert.Contains("add-pr", subcommands);
        Assert.Contains("normalize", subcommands);
        Assert.Contains("list", subcommands);
        Assert.Contains("show", subcommands);
        Assert.Contains("status", subcommands);
        Assert.Contains("batch-hydrate", subcommands);
    }

    [Fact]
    public void CorpusCommand_HasAllAnalysisCommands()
    {
        var cmd = CorpusCommand.Create();
        var subcommands = cmd.Subcommands.Select(s => s.Name).ToList();

        Assert.Contains("discover", subcommands);
        Assert.Contains("run", subcommands);
        Assert.Contains("run-all", subcommands);
        Assert.Contains("score", subcommands);
        Assert.Contains("report", subcommands);
    }

    [Fact]
    public void CorpusCommand_HasAllLabelingCommands()
    {
        var cmd = CorpusCommand.Create();
        var subcommands = cmd.Subcommands.Select(s => s.Name).ToList();

        Assert.Contains("label", subcommands);
        Assert.Contains("label-all", subcommands);
        Assert.Contains("reset-stats", subcommands);
    }

    [Fact]
    public void CorpusCommand_HasAllUtilityCommands()
    {
        var cmd = CorpusCommand.Create();
        var subcommands = cmd.Subcommands.Select(s => s.Name).ToList();

        Assert.Contains("purge", subcommands);
        Assert.Contains("errors", subcommands);
        Assert.Contains("rejected-repos", subcommands);
        Assert.Contains("doctor", subcommands);
    }

    [Fact]
    public void CorpusCommand_HasSubcommandGroups()
    {
        var cmd = CorpusCommand.Create();
        var subcommands = cmd.Subcommands.Select(s => s.Name).ToList();

        Assert.Contains("issues", subcommands);
        Assert.Contains("maintainers", subcommands);
    }
}
