// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns for detecting specific code constructs and markers.
/// Used by multiple secondary rules for semantic analysis.
/// </summary>
internal static class CodePatterns
{
    /// <summary>
    /// Development-only markers indicating temporary or dev-specific code.
    /// Used by GCI0003 (Behavioral Change) to avoid flagging dev-only logic.
    /// </summary>
    public static readonly string[] DevOnlyMarkers =
    [
        "// TODO", "// FIXME", "// HACK", "// TEMP",
        "// DEBUG", "// DEVELOPMENT ONLY", "#if DEBUG",
        "[Conditional(\"DEBUG\")]", "Environment.GetEnvironmentVariable(\"DEBUG\")"
    ];

    /// <summary>
    /// Framework initialization patterns (module init, app startup).
    /// Used by GCI0003 (Behavioral Change) to avoid flagging framework initialization code.
    /// </summary>
    public static readonly string[] FrameworkInitializationPatterns =
    [
        "builder.Services", "services.AddScoped", "Program.cs", "Startup.cs",
        "Configure(", "UseMiddleware", "ConfigureServices", "Module.Initialize",
        "app.UseRouting", "app.MapControllers", "endpoints.MapControllers"
    ];

    /// <summary>
    /// ORM auto-mapping patterns (Entity Framework, Dapper, etc.).
    /// Used by GCI0015 (Data Integrity) to avoid flagging ORM auto-mapping.
    /// </summary>
    public static readonly string[] OrmAutoMapPatterns =
    [
        ".ProjectTo<", ".Map", ".CreateMap<", "DbSet<",
        ".FromSql(", ".SQL(", "QueryAsync", ".Include(",
        "AutoMapper", "Dapper", ".AsTracking(", ".AsNoTracking()"
    ];

    /// <summary>
    /// DTO and data transfer object mapping patterns (AutoMapper, Mapster, etc.).
    /// Used by GCI0015 (Data Integrity) to avoid flagging DTO mapping.
    /// </summary>
    public static readonly string[] DtoMapperPatterns =
    [
        "Mapper.Map<", ".Map<", "CreateMap", "ForMember",
        "AutoMapper", "Mapster", "IMapper", "IMappingEngine",
        ".ProjectTo<", "TypeAdapter"
    ];

    /// <summary>
    /// Retry and resilience policy patterns (Polly, Resilience Pipeline).
    /// Used by GCI0032 (Exception Paths) to avoid flagging retry policies.
    /// </summary>
    public static readonly string[] RetryPolicyPatterns =
    [
        "Policy.Handle<", ".WaitAndRetry", ".Retry",
        "CircuitBreaker", "Polly", ".AddPolicyHandler",
        ".AddResilienceHandler", "ResilienceStrategy"
    ];

    /// <summary>
    /// Builder and fluent API patterns that guarantee non-null results.
    /// Used by GCI0043 (Nullability) to avoid flagging builder patterns.
    /// </summary>
    public static readonly string[] BuilderPatterns =
    [
        ".Build()", ".Create()", "Builder<", ".Build",
        "FluentBuilder", ".Configure(", "options.Configure",
        "HttpClientBuilder", "ServiceBuilder"
    ];

    /// <summary>
    /// Explicit interface implementation patterns.
    /// Used by GCI0047 (Naming) to avoid flagging interface implementations.
    /// </summary>
    public static readonly string[] InterfaceImplementationPatterns =
    [
        "void I", "string I", "bool I", "int I", "I.*\\..*\\(",
        ". I", "explicit interface", "interface I"
    ];

    /// <summary>
    /// Infrastructure and utility layer markers.
    /// Used by GCI0035 (Architecture) to avoid flagging infrastructure code.
    /// </summary>
    public static readonly string[] InfrastructureMarkers =
    [
        "/Infrastructure/", "/Configuration/", "/Startup",
        "ServiceExtensions", "AuthExtensions", "Program.cs",
        "CompositionRoot", "ModuleInitializer", "AppDefaults"
    ];

    /// <summary>
    /// Internal/private API markers indicating non-public code.
    /// Used by GCI0004 (Breaking Change) to avoid flagging internal API deprecations.
    /// </summary>
    public static readonly string[] InternalMarkers =
    [
        " internal ", "internal class", "internal interface", "internal struct",
        "internal enum", "internal record", "private class", "private interface",
        "namespace.*\\.Internal", "/Internal/", "InternalApi"
    ];

    /// <summary>
    /// DI composition root patterns indicating intentional container setup code.
    /// Used by GCI0038 (DI Safety) to avoid flagging composition root service locators.
    /// </summary>
    public static readonly string[] DiCompositionRootMarkers =
    [
        "CompositionRoot", "ConfigureServices", "AddApplicationServices",
        "services.AddScoped", "services.AddSingleton", "services.AddTransient",
        "builder.Services", "serviceCollection.Add", "container.Register"
    ];

    /// <summary>
    /// ORM async data access patterns: raw SQL async database operations with proper ConfigureAwait.
    /// Used by GCI0016 (Concurrency) and GCI0020 (Resource Exhaustion) to skip false positives on legitimate async ORM.
    /// </summary>
    public static readonly string[] OrmAsyncPatterns =
    [
        "ExecuteNonQueryAsync(", "ExecuteScalarAsync(", "ExecuteReaderAsync(",
        "SqlCommand", "SqliteCommand", "SqlDataReader"
    ];

    /// <summary>
    /// Fire-and-forget background task patterns indicating intentional non-awaited async operations.
    /// Used by GCI0020 (Resource Exhaustion) to skip false positives on intentional background telemetry.
    /// </summary>
    public static readonly string[] FireAndForgetBackgroundPatterns =
    [
        "_ = Task.Run", "_ = asyncMethod", "#pragma warning disable CS4014",
        ".LogAsync(", ".FlushAsync(", "BackgroundTaskQueue"
    ];

    /// <summary>
    /// Bounded synchronization patterns indicating controlled synchronization with limits.
    /// Used by GCI0016 (Blocking Async) to suppress false positives on intentional synchronization with bounds.
    /// </summary>
    public static readonly string[] BoundedSynchronizationPatterns =
    [
        "Semaphore", "Mutex", "SemaphoreSlim", "SpinLock",
        "ReaderWriterLockSlim", "Monitor.Enter", "lock ("
    ];

    /// <summary>
    /// Instance-scoped cache patterns indicating per-instance cache with bounded growth.
    /// Used by GCI0020 (Resource Exhaustion) to suppress false positives on intentional instance caches.
    /// </summary>
    public static readonly string[] InstanceScopedCachePatterns =
    [
        "private Dictionary", "private ConcurrentDictionary", "private Cache",
        "private readonly Dictionary", "_cache = new Dictionary",
        "this.cache", "field Dictionary"
    ];
}
