// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Resource and timeout patterns at module level, plus module-level constants for secret detection.
/// </summary>
internal static class ResourceTimeoutPatterns
{
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
}
