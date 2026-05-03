// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

using System.Text.RegularExpressions;

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect resource lifecycle and disposal issues.
/// </summary>
internal static class ResourcePatterns
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
    /// - GCI0039 (External Service Safety) owns HTTP/gRPC service clients
    /// - GCI0020 (Resource Exhaustion) owns timeout/resource limit types
    /// </summary>
    public static readonly HashSet<string> OwnedByOtherRules = new(StringComparer.Ordinal)
    {
        // GCI0039 (External Service Safety) - HTTP and service client types
        "HttpClient", "HttpClientHandler", "SocketsHttpHandler", "WebRequestHandler",
        "GrpcChannel", "Channel", // gRPC channels
        
        // GCI0020 (Resource Exhaustion) - Timeout and resource management types
        "Timer", "TimerCallback", "ElapsedEventHandler",
        "CancellationTokenSource", // Token sources are lifecycle-managed
        "ThreadPool", // Thread pool management is handled by GCI0020
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
