// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

using System.Text.RegularExpressions;
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Specialized domain-specific patterns: performance, floating-point, data integrity, PII detection,
/// idempotency, resources, exceptions, DI, stubs, and architecture validation.
/// </summary>
internal static class DomainSpecificPatterns
{
    // ================= Resource and Timeout Patterns =================
    
    /// <summary>
    /// Patterns indicating resource timeout limits in code.
    /// Used by GCI0020 for detecting timeout removal that could lead to resource exhaustion.
    /// </summary>
    public static readonly string[] TimeoutPatterns =
    [
        "timeout", "TimeSpan", "TimeoutException", "maxwait", "delay"
    ];

    /// <summary>
    /// Patterns indicating iteration or loop count limits in code.
    /// Used by GCI0020 for detecting iteration limit removal.
    /// </summary>
    public static readonly string[] IterationLimitPatterns =
    [
        "maxiterations", "max_iterations", "iterationcount", "iteration_count",
        "loopcount", "loop_count", "maxcount", "max_count", "limit"
    ];

    /// <summary>
    /// Patterns indicating resource limits (connections, threads, buffers, pools).
    /// Used by GCI0020 for detecting dangerous resource limit increases.
    /// </summary>
    public static readonly string[] ResourceLimitPatterns =
    [
        "maxconnections", "max_connections", "max_threads", "maxthreads",
        "poolsize", "pool_size", "buffersize", "buffer_size", "maxbuffer"
    ];

    /// <summary>
    /// Patterns indicating resource cleanup/disposal operations.
    /// Used by GCI0020 for detecting removal of resource cleanup code.
    /// </summary>
    public static readonly string[] ResourceCleanupPatterns =
    [
        "using (", "using(", "Dispose(", "dispose(", "Close()", "close()"
    ];

    /// <summary>
    /// Patterns indicating asynchronous operations that can consume resources.
    /// Used by GCI0020 for detecting unbounded async operations.
    /// </summary>
    public static readonly string[] AsyncPatterns =
    [
        "Task.Run", "Task.Factory", "await", "Parallel.For", "ThreadPool.QueueUserWorkItem"
    ];

    // ================= Test Patterns =================

    /// <summary>
    /// Test silence/skip patterns that prevent tests from running.
    /// Used by GCI0041 for detecting disabled or skipped tests that may hide regressions.
    /// </summary>
    public static readonly string[] TestSilencePatterns =
    [
        "[Ignore]", "[Ignore(", "[Skip]", "[Skip(", ".Skip(", "[Fact(Skip", "[Theory(Skip"
    ];

    /// <summary>
    /// Test attribute markers that identify test methods.
    /// Used by GCI0041 for detecting uninformative test method names.
    /// </summary>
    public static readonly string[] TestAttributeMarkers =
    [
        "[Fact]", "[Theory]", "[Test]"
    ];

    /// <summary>
    /// Assertion keywords used across popular .NET testing frameworks.
    /// Includes xUnit, NUnit, MSTest, FluentAssertions, Shouldly, Moq, NSubstitute, Playwright, etc.
    /// Used by GCI0041 for detecting test methods with missing assertions.
    /// </summary>
    public static readonly string[] TestAssertionKeywords =
    [
        // xUnit / NUnit / MSTest
        "Assert.", "Xunit.Assert", "NUnit.Framework.Assert",
        // Bare Assert() call (no dot): MongoDB, classic NUnit style
        "Assert(",
        // FluentAssertions / Shouldly
        "Should", ".ShouldBe", ".ShouldNotBe", ".ShouldBeNull", ".ShouldNotBeNull",
        ".Must(",
        // NSubstitute
        "Received(", "DidNotReceive(",
        // Moq / FakeItEasy
        ".Verify(", ".VerifyAll(", "MustHaveHappened", "MustNotHaveHappened",
        // Common assertion patterns
        "Throws<", "DoesNotThrow", "ThrowsAsync", "ThrowsExceptionAsync", "expect(", "Expect(",
        "IsTrue(", "IsFalse(", "IsNull(", "IsNotNull(", "AreEqual(", "AreNotEqual(",
        "Contains(", "IsInstanceOf",
        // Visual comparison / image assertions (ImageSharp etc.)
        ".CompareToReferenceOutput(",
        // Azure Provisioning test comparisons and SDK / validation helpers
        ".Compare(", ".ValidateAsync(", ".Lint(",
        // Selenium / Playwright browser integration tests (ASP.NET Core E2E)
        "Browser.",
        // Event-driven async tests: validates via TaskCompletionSource completion
        "TaskCompletionSource",
    ];

    // ================= Data Integrity Patterns =================

    /// <summary>
    /// Patterns used to detect data integrity risks, unsafe input handling, and conflicting data operations.
    /// </summary>
    public static class DataIntegrityPatterns
    {
        /// <summary>
        /// HTTP context signals indicating user input boundaries.
        /// Used by GCI0015 for detecting mass-assignment and unsafe casting in HTTP request context.
        /// </summary>
        public static readonly string[] HttpContextSignals =
        [
            "Request.Form", "Request.Query", "Request.Body",
            "HttpContext.Request", "[FromBody]", "[FromForm]", "[FromQuery]"
        ];

        /// <summary>
        /// SQL patterns that silently ignore or suppress insert/update conflicts.
        /// Used by GCI0015 to detect situations where data integrity violations are hidden.
        /// These are PATTERN STRINGS, not actual SQL commands - no GCI0015 violation.
        /// GCI0015 false positive suppression: this is pattern data for a detection rule.
        /// </summary>
        #pragma warning disable GCI0015  // Data Integrity Risk - pattern data only
        public static readonly string[] SqlIgnorePatterns =
        [
            "INSERT IGNORE", "ON CONFLICT DO NOTHING", "INSERT OR IGNORE"
        ];
        #pragma warning restore GCI0015

        /// <summary>
        /// Numeric cast patterns that can cause silent data truncation or overflow.
        /// Used by GCI0015 for detecting unchecked casts on potentially user-supplied values.
        /// These are PATTERN STRINGS, not actual casts - no GCI0015 violation.
        /// GCI0015 false positive suppression: this is pattern data for a detection rule.
        /// </summary>
        #pragma warning disable GCI0015  // Data Integrity Risk - pattern data only
        public static readonly string[] UncheckedCastPatterns =
        [
            "(int)", "(long)", "(decimal)", "(float)", "(short)"
        ];
        #pragma warning restore GCI0015

        /// <summary>
        /// Returns true if the given content contains an HTTP context signal indicating user input.
        /// </summary>
        public static bool HasHttpContextSignal(string content)
        {
            return HttpContextSignals.Any(signal => content.Contains(signal, StringComparison.Ordinal));
        }
    }

    // ================= PII Detection Patterns =================

    /// <summary>
    /// Patterns used to detect PII (Personally Identifiable Information) leaks in logs and transformations.
    /// </summary>
    public static class PiiDetectionPatterns
    {
        /// <summary>
        /// PII (Personally Identifiable Information) terms in variable/field names.
        /// Used by GCI0029 to detect leaks of sensitive data in log calls.
        /// Compound terms only (avoids false positives on "name", "fullname" which are ubiquitous).
        /// </summary>
        public static readonly string[] PiiTerms =
        [
            "email", "ssn", "socialsecurity", "phonenumber", "creditcard", "cardnumber",
            "dateofbirth", "passport", "nationalid", "taxid", "bankaccount",
            "dob", "birthdate", "zipcode", "postalcode", "geolocation",
            "username", "firstname", "lastname", "displayname", "personname",
        ];

        /// <summary>
        /// Logger method prefixes indicating logging calls.
        /// Used by GCI0029 to detect log statements for PII leak analysis.
        /// </summary>
        public static readonly string[] LogPrefixes =
        [
            "_logger.", "logger.", "Logger.", "_log.", "log.", "Log.Information", "Log.Warning",
            "Log.Error", "Log.Debug", "Log.Critical", "Log.Write"
        ];

        /// <summary>
        /// Data transformation and anonymization patterns indicating safe handling of PII.
        /// Used by GCI0029 to skip flagging data that has been hashed, encrypted, or anonymized.
        /// </summary>
        public static readonly string[] TransformationPatterns =
        [
            "Hash", "hash", "SHA", "HMAC", "MD5", "SHA256",
            "Token", "token", "anonymize", "Anonymize", "redact", "Redact",
            "Encrypt", "encrypt", "SecureString", "Mask", "mask"
        ];

        /// <summary>
        /// .NET reflection patterns indicating type inspection or metadata access.
        /// These are ubiquitous in .NET code and are NOT person data.
        /// Used by GCI0029 to skip flagging reflection properties that are commonly logged.
        /// </summary>
        public static readonly string[] ReflectionGuards =
        [
            ".FullName", ".Name", "Type.", "Assembly.", "PropertyInfo.", "MethodInfo.",
            "FieldInfo.", "ParameterInfo.", "Reflection.",
            // Additional reflection patterns
            "GetType(", "typeof(", "GetProperties", "GetFields", "GetMethods",
            "MemberInfo", "CustomAttributes", "GetCustomAttributes",
            "MethodBase", "ConstructorInfo", "EventInfo",
            // Logging metadata types
            "LogLevel", "LogEventInfo", "LogEventLevel", "EventId",
            // Serialization/deserialization contexts
            "SerializationContext", "DeserializationContext", "JsonSerializerContext",
            "JsonPropertyInfo", "TypeInfo", "MethodHandle"
        ];

        /// <summary>
        /// Returns true if content indicates the data is being transformed (hashed, encrypted, anonymized).
        /// </summary>
        public static bool IsDataTransformed(string content)
        {
            if (TransformationPatterns.Any(p => content.Contains(p))) return true;
            if (ReflectionGuards.Any(p => content.Contains(p))) return true;
            return false;
        }
    }

    // ================= Idempotency and Retry Patterns =================

    /// <summary>
    /// Patterns used to detect idempotency and retry safety issues.
    /// </summary>
    public static class IdempotencyPatterns
    {
        /// <summary>
        /// Idempotency key signals (headers, parameters, field names) indicating idempotent request handling.
        /// Used by GCI0022 to detect HTTP POST endpoints with idempotency key support.
        /// </summary>
        public static readonly string[] IdempotencySignals =
        [
            "IdempotencyKey", "Idempotency-Key", "idempotencyKey", "idempotent",
            "dedup", "Dedup", "RequestId", "requestId", "MessageId", "messageId",
            // Additional patterns for common retry frameworks
            "duplicateCheck", "checkDuplicate", "DuplicateKey", "duplicate_key",
            "uniqueRequest", "UniqueRequest", "uniqueId", "UniqueId",
            "idempotencyMode", "RequestHash", "request_hash", "traceId"
        ];

        /// <summary>
        /// SQL/database upsert patterns indicating conflict resolution for duplicate inserts.
        /// Used by GCI0022 to detect raw INSERT statements without upsert guards.
        /// </summary>
        public static readonly string[] UpsertPatterns =
        [
            "ON DUPLICATE KEY", "ON CONFLICT", "INSERT OR REPLACE",
            "INSERT OR IGNORE", "MERGE INTO", "UPSERT"
        ];
    }

    // ================= Resource Lifecycle Patterns =================

    /// <summary>
    /// Patterns used to detect resource lifecycle and disposal issues.
    /// </summary>
    public static class ResourcePatterns
    {
        /// <summary>
        /// Known disposable types that should be used in using statements or try/finally.
        /// Used by GCI0024 to detect unguarded resource allocations.
        /// </summary>
        public static readonly string[] DisposableTypes =
        [
            "new FileStream(", "new StreamWriter(", "new StreamReader(", "new MemoryStream(",
            "new SqlConnection(", "new SqlCommand(", "new SqlDataReader(",
            "new HttpClient(", "new TcpClient(", "new UdpClient(", "new Socket(",
            "new Mutex(", "new Semaphore(", "new SemaphoreSlim(",
            "new EventWaitHandle(", "new ManualResetEvent(",
            "new BinaryWriter(", "new BinaryReader(",
            "new GZipStream(", "new DeflateStream(", "new CryptoStream(",
            "new X509Certificate(", "new RSACryptoServiceProvider("
        ];

        /// <summary>
        /// Type name suffixes indicating disposable resources (suffix-based heuristic).
        /// Used by GCI0024 to catch any type whose name ends with these patterns.
        /// </summary>
        public static readonly string[] DisposableSuffixes =
        [
            "Stream", "Reader", "Writer", "Connection", "Client",
            "Listener", "Channel", "Context", "Provider", "Session", "Transaction",
            "Certificate", "Timer"
        ];

        /// <summary>
        /// Types whose lifecycle detection is owned by other rules (suppress in GCI0024 to avoid double-reporting).
        /// Used by GCI0024 for disposal suppression (these are IDisposable but managed by other rules).
        /// </summary>
        public static readonly HashSet<string> OwnedByOtherRules = new(StringComparer.Ordinal)
        {
            "HttpClient", // Owned by GCI0039 (External Service Safety)
        };

        /// <summary>
        /// Known non-disposable types with "Context" or similar suffixes (false positive suppression).
        /// Used by GCI0024 to avoid flagging context types that appear disposable but are not.
        /// </summary>
        public static readonly HashSet<string> KnownNonDisposableTypes = new(StringComparer.Ordinal)
        {
            // Microsoft.CodeAnalysis / Roslyn analysis context types
            "SyntaxContext", "AnalysisContext", "SemanticContext",
            "SyntaxNodeAnalysisContext", "OperationAnalysisContext", "CodeBlockAnalysisContext",
            // System.CommandLine types
            "InvocationContext",
            // ASP.NET Core filter/action context types
            "HttpContext", "RouteContext", "FilterContext", "ActionContext",
            "AuthorizationFilterContext", "ResourceExecutingContext", "ResourceExecutedContext",
            "ResultExecutingContext", "ResultExecutedContext", "ExceptionContext",
            // Other common non-disposable context types
            "ValidationContext", "NavigationContext",
            // OpenTelemetry value types
            "PropagationContext", "ActivityContext", "SpanContext",
            // FluentAssertions comparison context types
            "MemberSelectionContext", "EquivalencyValidationContext", "CreatorPropertyContext",
            "StrategyBuilderContext", "SelectionContext",
            // WPF/WinForms SynchronizationContext
            "SynchronizationContext", "DispatcherSynchronizationContext",
            "DispatcherQueueSynchronizationContext",
            // Logging/diagnostic adapter scopes
            "LoggingAdapterScope", "LoggerScope", "DiagnosticScope", "ActivityScope",
            // Enumerators: typically short-lived value types
            "Enumerator", "WhiteSpaceSegmentEnumerator", "TokenEnumerator",
        };

        /// <summary>
        /// Regex pattern to extract type names from "new Type(...)" instantiations.
        /// Used by GCI0024 to match dynamically allocated resource types.
        /// </summary>
        public static readonly Regex NewTypeRegex =
            new(@"new ([A-Z][A-Za-z0-9]+)\(", RegexOptions.Compiled);
    }

    // ================= External Service Patterns =================

    /// <summary>
    /// Patterns used to detect external service and HTTP client safety issues.
    /// </summary>
    public static class ExternalServicePatterns
    {
        /// <summary>
        /// HTTP method calls (on HttpClient or HttpRequestMessage) that should have timeouts and cancellation tokens.
        /// Used by GCI0039 to detect unsafe external service calls.
        /// </summary>
        public static readonly string[] HttpCallMethods =
        [
            ".GetAsync(", ".PostAsync(", ".PutAsync(", ".DeleteAsync(", ".SendAsync("
        ];

        /// <summary>
        /// Subset of HTTP methods for cancellation token checking (excludes DeleteAsync which conflicts with SDK methods).
        /// Used by GCI0039 to detect missing CancellationToken parameters on HTTP calls.
        /// </summary>
        public static readonly string[] CtCheckHttpMethods =
        [
            ".GetAsync(", ".PostAsync(", ".PutAsync(", ".SendAsync("
        ];

        /// <summary>
        /// Polly resilience patterns that indicate retry/timeout policies are already in place.
        /// Used by GCI0039 to skip flagging HTTP calls when Polly policies manage timeouts.
        /// </summary>
        public static readonly string[] PollyPatterns =
        [
            ".WaitAndRetry", ".CircuitBreaker", ".Timeout", "TimeoutPolicy",
            "PolicyBuilder", ".AddPolicyHandler", "AddResilienceHandler"
        ];

        /// <summary>
        /// HttpClientFactory configuration patterns indicating managed HTTP clients with centralized timeout configuration.
        /// Used by GCI0039 to skip timeout checks on factory-configured clients.
        /// </summary>
        public static readonly string[] FactoryConfigPatterns =
        [
            "AddHttpClient", "IHttpClientFactory", "CreateClient", "typed client",
            "ConfigureHttpClient", "AddHttpMessageHandler"
        ];

        /// <summary>
        /// Fire-and-forget async patterns where cancellation tokens are not applicable.
        /// Used by GCI0039 to skip CancellationToken requirements on intentional fire-and-forget operations.
        /// </summary>
        public static readonly string[] FireAndForgetPatterns =
        [
            "_ =", ".FireAndForget", "#pragma warning disable",
            ".Forget(", "// fire and forget"
        ];
    }

    // ================= Performance Patterns =================

    /// <summary>
    /// Patterns used to detect performance hotpath issues (LINQ in loops, Thread.Sleep, etc.).
    /// </summary>
    public static class PerformancePatterns
    {
        /// <summary>LINQ method calls that should not be used inside loops.</summary>
        public static readonly string[] LinqMethods =
        [
            ".Where(", ".Select(", ".FirstOrDefault(", ".Any(", ".Count("
        ];

        /// <summary>Loop keywords that should not contain blocking operations or unbounded operations.</summary>
        public static readonly string[] LoopKeywords =
        [
            "for (", "foreach (", "while ("
        ];

        /// <summary>Loop keywords where unbounded collection growth is a concern (for/while, not foreach).</summary>
        public static readonly string[] UnboundedLoopKeywords =
        [
            "for (", "while ("
        ];

        /// <summary>Returns <c>true</c> if the given content contains a LINQ method call.</summary>
        public static bool HasLinqCall(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            // GCI0044: This is a pattern detection helper, not a performance-sensitive query
            return LinqMethods.Any(m => content.Contains(m, StringComparison.Ordinal));
        }

        /// <summary>Returns <c>true</c> if the given content contains a loop construct.</summary>
        public static bool HasLoopConstruct(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            return LoopKeywords.Any(k => content.Contains(k, StringComparison.Ordinal));
        }

        /// <summary>Returns <c>true</c> if the given path is a rule implementation file (hotpath guard).</summary>
        public static bool IsRuleImplementationFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.Contains("Rules/Implementations", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains(@"Rules\Implementations", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ================= Floating-Point Patterns =================

    /// <summary>
    /// Floating-point literal and cast patterns used to detect unsafe equality comparisons.
    /// </summary>
    public static class FloatingPointPatterns
    {
        /// <summary>Regex: matches == or != followed by a float/double literal on the right side.</summary>
        public static readonly Regex FloatLiteralOnRightRegex = new(
            @"(?:==|!=)\s*(?:[-+]?\d*\.\d+|\d+\.\d+)[fFdD]?\b",
            RegexOptions.Compiled);

        /// <summary>Regex: matches float/double literal on the left side of == or !=.</summary>
        public static readonly Regex FloatLiteralOnLeftRegex = new(
            @"\b(?:[-+]?\d*\.\d+|\d+\.\d+)[fFdD]?\s*(?:==|!=)",
            RegexOptions.Compiled);

        /// <summary>Regex: matches a (float) or (double) cast alongside == or !=.</summary>
        public static readonly Regex FloatCastWithEqualityRegex = new(
            @"\((?:float|double)\).*(?:==|!=)|(?:==|!=).*\((?:float|double)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Regex: matches a float or double type keyword alongside == or !=.</summary>
        public static readonly Regex FloatTypeWithEqualityRegex = new(
            @"\b(?:float|double)\b.*(?:==|!=)|(?:==|!=).*\b(?:float|double)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Regex: matches the safe-division guard pattern (integer zero-check with ternary).</summary>
        public static readonly Regex IntegerZeroGuardRegex = new(
            @"(?:==|!=)\s*0\s*\?", RegexOptions.Compiled);

        /// <summary>Returns <c>true</c> if the given content is a guarded integer zero check (safe division pattern).</summary>
        public static bool IsGuardedIntegerZeroCheck(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            return IntegerZeroGuardRegex.IsMatch(content);
        }
    }

    // ================= Data Schema Patterns =================

    /// <summary>
    /// Patterns used to detect data schema and serialization compatibility issues.
    /// </summary>
    public static class DataSchemaPatterns
    {
        /// <summary>
        /// Serialization and schema-mapping attributes that indicate a field is part of a wire format
        /// or persistent storage contract. Removal breaks deserialization of existing data.
        /// Used by GCI0021 to detect removed serialization attributes.
        /// </summary>
        public static readonly string[] SerializationAttributes =
        [
            "[JsonProperty", "[JsonPropertyName", "[Column(", "[DataMember",
            "[BsonElement", "[Key]", "[ForeignKey", "[Required]", "[MaxLength"
        ];
    }

    // ================= Exception Patterns =================

    /// <summary>
    /// Patterns used to detect uncaught exception paths and exception handling issues.
    /// </summary>
    #pragma warning disable GCI0032  // Uncaught Exception Path - pattern data only
    public static class ExceptionPatterns
    {
        /// <summary>
        /// Test assertion methods that validate exception handling (Assert.Throws, Should().Throw, etc.).
        /// Used by GCI0032 to determine whether throw new statements are covered by tests.
        /// These are PATTERN STRINGS for exception detection, not actual exception throws - no GCI0032 violation.
        /// </summary>
        public static readonly string[] ThrowAssertions =
        [
            "Assert.Throws", ".Should().Throw", "ThrowsAsync", "ThrowsExceptionAsync", "Throws<"
        ];

        /// <summary>
        /// Guard clause throws (ArgumentNullException, etc.) that are defensive programming patterns
        /// and do not require test coverage in the same diff (they protect preconditions, not logic paths).
        /// Used by GCI0032 to exclude guard clause throws from uncaught exception detection.
        /// These are PATTERN STRINGS for exception pattern matching, not actual throws - no GCI0032 violation.
        /// </summary>
        public static readonly string[] GuardClauseThrows =
        [
            "throw new ArgumentNullException",
            "throw new ArgumentException",
            "throw new ArgumentOutOfRangeException",
            "throw new ObjectDisposedException",
            "throw new InvalidOperationException",
            "throw new NotSupportedException",
            "throw new FormatException",
            "throw new IndexOutOfRangeException",
            "throw new KeyNotFoundException",
            "throw new UnauthorizedAccessException",
        ];
    }
    #pragma warning restore GCI0032

    // ================= Dependency Injection Patterns =================

    /// <summary>
    /// Patterns used to detect dependency injection anti-patterns and safety issues.
    /// </summary>
    public static class DependencyInjectionPatterns
    {
        /// <summary>
        /// Service locator anti-patterns (Service.Current, ServiceLocator.GetInstance, etc.).
        /// Service locator hides dependencies and makes testing harder.
        /// Used by GCI0038 to detect service locator usage in non-infrastructure code.
        /// </summary>
        public static readonly string[] ServiceLocatorPatterns =
        [
            "Service.Current", "ServiceLocator.GetInstance", "ServiceProvider.GetService",
            "Container.Resolve", "ObjectFactory.GetInstance", "ObjectFactory.Create",
            "Globals.ThisAddIn", "Globals.Ribbon",
            ".GetRequiredService<", ".GetService<"
        ];

        /// <summary>
        /// Patterns to exclude from direct instantiation detection (factories, singletons, registrations, etc.).
        /// These are legitimate uses of 'new' that should not trigger GCI0038 false positives.
        /// </summary>
        public static readonly string[] DirectInstantiationExclusions =
        [
            "new ServiceCollection", "AddScoped<", "AddSingleton<", "AddTransient<",
            "RegisterService", "RegisterSingleton", "RegisterScoped", "RegisterTransient",
            "new object()", "new List<", "new Dictionary<", "new HashSet<", "new []", "new [",
            "factory", "Factory", "builder", "Builder", "provider", "Provider",
            "EventHandler", "Delegate", "Action", "Func"
        ];

        /// <summary>
        /// Regex: matches "new TypeName(...)" patterns to detect direct instantiation of service types.
        /// Used by GCI0038 to identify services being directly instantiated instead of injected.
        /// </summary>
        public static readonly Regex DirectInstantiationRegex =
            new(@"new\s+([A-Z][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

        /// <summary>
        /// Returns <c>true</c> if the given path is an infrastructure or DI setup file.
        /// Infrastructure files (Startup, ServiceCollectionExtensions, DI containers) use direct
        /// instantiation and service locator patterns as part of their job and should be excluded.
        /// </summary>
        public static bool IsInfrastructureFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            
            var lowerPath = path.Replace('\\', '/').ToLowerInvariant();
            
            // DI container and startup files
            return lowerPath.Contains("startup") ||
                   lowerPath.Contains("servicecollection") ||
                   lowerPath.Contains("dependencyinjection") ||
                   lowerPath.Contains("dicontainer") ||
                   lowerPath.Contains("extensions.cs") || // ServiceExtensions, AuthExtensions, etc.
                   lowerPath.Contains("/infrastructure/", StringComparison.OrdinalIgnoreCase) ||
                   lowerPath.Contains("/configuration/", StringComparison.OrdinalIgnoreCase) ||
                   lowerPath.Contains("program.cs") ||
                   lowerPath.Contains("composition");
        }
    }

    // ================= Stub and TODO Detection Patterns =================

    /// <summary>
    /// Patterns used to detect TODO/stub markers and incomplete code.
    /// </summary>
    public static class StubDetectionPatterns
    {
        /// <summary>
        /// Stub marker keywords (TODO, FIXME, HACK) that indicate incomplete code requiring resolution before production.
        /// Used by GCI0042 to detect stub comments and incomplete implementations.
        /// </summary>
        public static readonly string[] StubKeywords = ["TODO", "FIXME", "HACK"];
    }

    // ================= Architecture Patterns =================

    /// <summary>
    /// Patterns used to detect architectural boundary violations and policy violations.
    /// </summary>
    public static class ArchitecturePatterns
    {
        /// <summary>
        /// Regex: matches C# using directives to extract the imported namespace.
        /// Used by GCI0035 to validate imports against configured forbidden import pairs.
        /// </summary>
        public static readonly Regex UsingRegex =
            new(@"^\s*using\s+([\w.]+)\s*;", RegexOptions.Compiled);
    }

    // ================= Framework Pattern Detection =================

    /// <summary>
    /// Patterns for detecting framework usage (MVVM, DI, HTTP frameworks, etc.)
    /// Used by multiple secondary rules for FP reduction.
    /// </summary>
    public static class FrameworkPatterns
    {
        /// <summary>
        /// MVVM framework patterns indicating ViewModel, data binding, or property notification code.
        /// Used by GCI0043 (Nullability) to avoid flagging ViewModels with null-forgiving operators.
        /// </summary>
        public static readonly string[] MvvmPatterns =
        [
            "ViewModelBase", "INotifyPropertyChanged", "RaisePropertyChanged",
            "NotifyPropertyChanged", "OnPropertyChanged", "SetProperty",
            "PropertyChanged", "BindableBase", ".PropertyChanged",
            "ICommand", "RelayCommand"
        ];

        /// <summary>
        /// Dependency injection container registration patterns.
        /// Used by GCI0035 (Architecture) to identify DI setup code that legitimately crosses layer boundaries.
        /// </summary>
        public static readonly string[] DiContainerPatterns =
        [
            "AddScoped", "AddSingleton", "AddTransient", "services.Add",
            "Register<", "RegisterSingleton", "RegisterScoped",
            "ConfigureServices", "GetRequiredService", "GetService<",
            "IServiceProvider", "IServiceCollection"
        ];

        /// <summary>
        /// HTTP/ASP.NET framework binding and mapping patterns.
        /// Used by GCI0015 (Data Integrity) to avoid flagging framework-managed HTTP binding.
        /// </summary>
        public static readonly string[] HttpBindingPatterns =
        [
            "[Bind(", "[FromBody]", "[FromQuery]", "[FromRoute]",
            "[ModelBinder(", "ModelBinder", "IModelBinder",
            "BindingSource", "HttpContext", "Request.Form"
        ];

        /// <summary>
        /// Exception handling framework patterns (log4net, Serilog, custom handlers).
        /// Used by GCI0032 (Exception Paths) to avoid flagging framework exception handlers.
        /// </summary>
        public static readonly string[] ExceptionHandlingPatterns =
        [
            "IExceptionHandler", "ExceptionHandler", "ExceptionFilter",
            "OnException", "ExceptionContext", "ILogger",
            "log.Error", "logger.Error", "LogError"
        ];
    }

    // ================= Test-Specific Patterns =================

    /// <summary>
    /// Patterns for detecting test-specific code and test infrastructure.
    /// Used by multiple secondary rules for FP reduction in test code paths.
    /// </summary>
    public static class TestPatterns
    {
        /// <summary>
        /// Test setup/teardown attributes that mark fixture initialization and cleanup.
        /// Used by GCI0032 (Exception Paths) to avoid flagging test teardown exception handlers.
        /// </summary>
        public static readonly string[] SetupTeardownAttributes =
        [
            "[Setup]", "[TearDown]", "[SetUp]", "[ClassInitialize]",
            "[ClassCleanup]", "[TestInitialize]", "[TestCleanup]",
            "[OneTimeSetUp]", "[OneTimeTearDown]", "[BeforeEach]", "[AfterEach]",
            "protected void SetUp", "protected void TearDown"
        ];

        /// <summary>
        /// Mock object creation patterns (Moq, NSubstitute, etc.).
        /// Used by GCI0032 (Exception), GCI0047 (Naming) to avoid flagging mock creation patterns.
        /// </summary>
        public static readonly string[] MockObjectPatterns =
        [
            "new Mock<", "Substitute.For<", "Create<", "Mock<",
            "Moq.Mock", "NSubstitute", ".Setup(", ".Returns(",
            "It.Is<", "It.IsAny<", "Arg.Any", "Arg.Is"
        ];

        /// <summary>
        /// Test fixture and builder setup patterns used in test infrastructure.
        /// Used by GCI0035 (Architecture) to avoid flagging test fixture setup code.
        /// </summary>
        public static readonly string[] TestFixturePatterns =
        [
            "TestFixture", "Fixture", "Builder", "WithFixture",
            "SetupFixture", "builder.With", ".Build()", "TestBase",
            "TestHelper", "ObjectMother", "Factory.Create"
        ];

        /// <summary>
        /// Assertion library method calls that suggest test code.
        /// Used by multiple rules to confirm test context.
        /// </summary>
        public static readonly string[] AssertionLibraryPatterns =
        [
            "Assert.That", "Assert.Throws", "Should().Throw", ".Should().",
            "Verify()", "Received()", ".Throws<", "Xunit.Assert",
            "FluentAssertions", "Shouldly", "Verify.That"
        ];
    }

    // ================= Code Pattern Detection =================

    /// <summary>
    /// Patterns for detecting specific code constructs and markers.
    /// Used by multiple secondary rules for semantic analysis.
    /// </summary>
    public static class CodePatterns
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
            "UploadInBackground", "UploadAsync", "Task.Run(", "ContinueWith(",
            "Telemetry", "telemetry", "Background", "background"
        ];

        /// <summary>
        /// Bounded synchronization collection patterns: semaphore/mutex/lock with bounded growth or scope limits.
        /// Used by GCI0016 (Concurrency) to skip false positives on intentional synchronization for bounded resources.
        /// </summary>
        public static readonly string[] BoundedSynchronizationPatterns =
        [
            "SemaphoreSlim", "Mutex", "lock (", "WithMutex(",
            "events.Count", "Skip("
        ];

        /// <summary>
        /// Instance-scoped cache patterns: non-static Dictionary fields indicating bounded, scope-limited caches.
        /// Used by GCI0020 (Resource Exhaustion) to skip false positives on intentional per-instance caches.
        /// </summary>
        public static readonly string[] InstanceScopedCachePatterns =
        [
            "private readonly Dictionary<", "private readonly Cache<",
            "_cache", "_Cache", "Cache", "TryGetValue(", "IReadOnlyList<"
        ];
    }

    // ================= Generic Helper Methods =================

    /// <summary>
    /// Returns <c>true</c> if the given HTTP request content contains HTTP context signal patterns.
    /// Used by GCI0015 to determine whether mass-assignment and unsafe cast checks apply.
    /// </summary>
    public static bool HasHttpContextSignal(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return DataIntegrityPatterns.HasHttpContextSignal(content);
    }

    /// <summary>
    /// Returns <c>true</c> if the content contains any dev-only markers.
    /// Used by GCI0003 to avoid flagging development-only code changes.
    /// </summary>
    public static bool HasDevOnlyMarker(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return CodePatterns.DevOnlyMarkers.Any(m => content.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <c>true</c> if the content contains any MVVM framework patterns.
    /// Used by GCI0043 to avoid flagging ViewModels with null-forgiving operators.
    /// </summary>
    public static bool HasMvvmPattern(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return FrameworkPatterns.MvvmPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <c>true</c> if the content contains ORM or DTO mapping patterns.
    /// Used by GCI0015 to avoid flagging ORM auto-mapping configurations.
    /// </summary>
    public static bool HasMappingPattern(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return CodePatterns.OrmAutoMapPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
               CodePatterns.DtoMapperPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <c>true</c> if the content contains mock object creation patterns.
    /// Used by GCI0047 and GCI0032 to avoid flagging test mocks and assertion helpers.
    /// </summary>
    public static bool HasMockPattern(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return TestPatterns.MockObjectPatterns.Any(p => content.Contains(p, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <c>true</c> if the line/path indicates internal or private API.
    /// Used by GCI0004 (Breaking Change) to avoid flagging internal API deprecations.
    /// </summary>
    public static bool HasInternalMarker(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return CodePatterns.InternalMarkers.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <c>true</c> if the line indicates DI composition root code.
    /// Used by GCI0038 (DI Safety) to avoid flagging intentional composition patterns.
    /// </summary>
    public static bool IsDiCompositionRoot(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return CodePatterns.DiCompositionRootMarkers.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}
