// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns for detecting framework usage (MVVM, DI, HTTP frameworks, etc.)
/// Used by multiple secondary rules for FP reduction.
/// </summary>
internal static class FrameworkPatterns
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
