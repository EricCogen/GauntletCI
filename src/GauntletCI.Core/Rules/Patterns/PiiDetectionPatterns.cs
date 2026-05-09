// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect PII (Personally Identifiable Information) leaks in logs and transformations.
/// </summary>
internal static class PiiDetectionPatterns
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
