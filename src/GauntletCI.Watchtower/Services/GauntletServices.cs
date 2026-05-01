using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GauntletCI.Watchtower.Services;

public class GauntletCommandRunner
{
    private readonly ILogger<GauntletCommandRunner> _logger;

    public GauntletCommandRunner(ILogger<GauntletCommandRunner> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Success, string Stdout, string Stderr, int ExitCode)> RunCommandAsync(
        string command,
        string arguments,
        string workingDirectory = ".",
        int timeoutSeconds = 300)
    {
        _logger.LogInformation("Executing: {Command} {Arguments} in {WorkingDir}", command, arguments, workingDirectory);

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process == null)
                {
                    _logger.LogError("Failed to start process: {Command}", command);
                    return (false, string.Empty, "Failed to start process", -1);
                }

                var stdout = new System.Text.StringBuilder();
                var stderr = new System.Text.StringBuilder();

                var outputTask = Task.Run(() =>
                {
                    string line;
                    while ((line = process.StandardOutput.ReadLine()) != null)
                    {
                        stdout.AppendLine(line);
                    }
                });

                var errorTask = Task.Run(() =>
                {
                    string line;
                    while ((line = process.StandardError.ReadLine()) != null)
                    {
                        stderr.AppendLine(line);
                    }
                });

                var timeoutHandle = TimeSpan.FromSeconds(timeoutSeconds);
                var completed = process.WaitForExit((int)timeoutHandle.TotalMilliseconds);

                if (!completed)
                {
                    _logger.LogWarning("Process timeout after {Seconds} seconds", timeoutSeconds);
                    process.Kill();
                    return (false, stdout.ToString(), $"Process timeout after {timeoutSeconds} seconds", -1);
                }

                Task.WaitAll(outputTask, errorTask);

                _logger.LogInformation("Process exited with code {ExitCode}", process.ExitCode);

                return (process.ExitCode == 0, stdout.ToString(), stderr.ToString(), process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Command}", command);
            return (false, string.Empty, ex.Message, -1);
        }
    }
}

public class GauntletSyncServiceImpl : IGauntletSyncService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GauntletSyncServiceImpl> _logger;
    private readonly GauntletCommandRunner _commandRunner;

    public GauntletSyncServiceImpl(IConfiguration configuration, ILogger<GauntletSyncServiceImpl> logger, GauntletCommandRunner commandRunner)
    {
        _configuration = configuration;
        _logger = logger;
        _commandRunner = commandRunner;
    }

    public async Task<string> SyncRepositoryAsync()
    {
        var repoUrl = _configuration.GetValue<string>("GauntletCI:RepositoryUrl") ?? "https://github.com/EricCogen/GauntletCI.git";
        var repoPath = _configuration.GetValue<string>("GauntletCI:RepositoryPath") ?? "./gauntletci-repo";
        var branch = _configuration.GetValue<string>("GauntletCI:Branch") ?? "main";

        _logger.LogInformation("Syncing GauntletCI repository from {RepoUrl}", repoUrl);

        // Create repo directory if it doesn't exist
        Directory.CreateDirectory(repoPath);

        string commitSha;

        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            // Clone the repository
            _logger.LogInformation("Cloning repository");
            var (success, stdout, stderr, exitCode) = await _commandRunner.RunCommandAsync("git", $"clone {repoUrl} .", repoPath);
            
            if (!success)
            {
                throw new Exception($"Failed to clone repository: {stderr}");
            }
        }

        // Fetch latest changes
        _logger.LogInformation("Fetching latest changes");
        var (fetchSuccess, _, fetchStderr, _) = await _commandRunner.RunCommandAsync("git", "fetch origin", repoPath);
        
        if (!fetchSuccess)
        {
            _logger.LogWarning("Fetch failed, continuing: {Error}", fetchStderr);
        }

        // Hard reset to remote branch
        _logger.LogInformation("Hard resetting to origin/{Branch}", branch);
        var (resetSuccess, _, resetStderr, _) = await _commandRunner.RunCommandAsync("git", $"reset --hard origin/{branch}", repoPath);
        
        if (!resetSuccess)
        {
            throw new Exception($"Failed to reset to origin/{branch}: {resetStderr}");
        }

        // Clean untracked files
        await _commandRunner.RunCommandAsync("git", "clean -fd", repoPath);

        // Get current commit SHA
        var (shaSuccess, shaOutput, shaSterr, _) = await _commandRunner.RunCommandAsync("git", "rev-parse HEAD", repoPath);
        
        if (!shaSuccess)
        {
            throw new Exception($"Failed to get commit SHA: {shaSterr}");
        }

        commitSha = shaOutput.Trim();
        _logger.LogInformation("Current commit SHA: {CommitSha}", commitSha);

        return commitSha;
    }
}

public class GauntletBuildServiceImpl : IGauntletBuildService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GauntletBuildServiceImpl> _logger;
    private readonly GauntletCommandRunner _commandRunner;

    public GauntletBuildServiceImpl(IConfiguration configuration, ILogger<GauntletBuildServiceImpl> logger, GauntletCommandRunner commandRunner)
    {
        _configuration = configuration;
        _logger = logger;
        _commandRunner = commandRunner;
    }

    public async Task<bool> BuildGauntletCiAsync(Models.Entities.GauntletRun gauntletRun)
    {
        var repoPath = _configuration.GetValue<string>("GauntletCI:RepositoryPath") ?? "./gauntletci-repo";
        var publishPath = _configuration.GetValue<string>("GauntletCI:PublishPath") ?? "./gauntletci-build/cli";

        _logger.LogInformation("Building GauntletCI from {CommitSha}", gauntletRun.CommitSha);

        try
        {
            // Clean
            _logger.LogInformation("Running dotnet clean");
            await _commandRunner.RunCommandAsync("dotnet", "clean -c Release -q", repoPath);

            // Restore
            _logger.LogInformation("Running dotnet restore");
            var (restoreSuccess, restoreStdout, restoreStderr, _) = await _commandRunner.RunCommandAsync("dotnet", "restore", repoPath, 300);
            
            if (!restoreSuccess)
            {
                gauntletRun.BuildOutput = restoreStderr;
                return false;
            }

            // Build
            _logger.LogInformation("Running dotnet build");
            var (buildSuccess, buildStdout, buildStderr, _) = await _commandRunner.RunCommandAsync("dotnet", "build -c Release -q", repoPath, 600);
            
            if (!buildSuccess)
            {
                gauntletRun.BuildOutput = buildStderr;
                return false;
            }

            // Publish CLI
            _logger.LogInformation("Publishing GauntletCI.Cli to {PublishPath}", publishPath);
            Directory.CreateDirectory(publishPath);
            
            var publishArgs = $"publish src/GauntletCI.Cli -c Release -o {publishPath} -q";
            var (publishSuccess, publishStdout, publishStderr, _) = await _commandRunner.RunCommandAsync("dotnet", publishArgs, repoPath, 600);
            
            if (!publishSuccess)
            {
                gauntletRun.BuildOutput = publishStderr;
                return false;
            }

            gauntletRun.BuildOutput = "Build successful";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build failed with exception");
            gauntletRun.BuildOutput = ex.Message;
            return false;
        }
    }
}
