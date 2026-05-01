using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GauntletCI.Watchtower.Data;
using GauntletCI.Watchtower.Services;
using GauntletCI.Watchtower;
using Serilog;

// Set up logging first
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/watchtower-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Watchtower service");

    var builder = Host.CreateDefaultBuilder(args);

    builder
        .ConfigureAppConfiguration((context, config) =>
        {
            config
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
        })
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            // Add logging
            services.AddLogging();

            // Add HttpClient for external API calls
            services.AddHttpClient();

            // Add command runner
            services.AddScoped<GauntletCommandRunner>();

            // Add DbContext
            var dbPath = context.Configuration.GetValue<string>("Database:Path") ?? "watchtower.db";
            services.AddDbContext<WatchtowerDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // Register services - order matters for dependency injection
            services.AddScoped<IAlertService, AlertService>();
            services.AddScoped<ICVEFeedService, CVEFeedService>();
            services.AddScoped<ITechnicalAnalysisService, TechnicalAnalysisService>();
            services.AddScoped<IGauntletSyncService, GauntletSyncServiceImpl>();
            services.AddScoped<IGauntletBuildService, GauntletBuildServiceImpl>();
            services.AddScoped<IValidationExecutor, ValidationExecutorImpl>();
            services.AddScoped<IResultParser, ResultParserImpl>();
            services.AddScoped<IArticleGeneratorService, ArticleGeneratorServiceImpl>();
            services.AddScoped<IGapAnalyzerService, GapAnalyzerServiceImpl>();
            services.AddScoped<IArticlePublisherService, ArticlePublisherServiceImpl>();
            services.AddScoped<IRunSummaryService, RunSummaryServiceImpl>();

            // Add hosted service (the main worker)
            services.AddHostedService<WatchtowerWorker>();
        })
        .Build()
        .Run();

    Log.Information("Watchtower service stopped successfully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Watchtower service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
