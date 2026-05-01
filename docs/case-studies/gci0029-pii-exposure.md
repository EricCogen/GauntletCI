# GCI0029 Case Study: PII Exposure via Logging

## The Problem

A SaaS platform logged all authentication attempts to help with debugging failed logins. The logging code was designed to be "transparent" - it would log the entire request and response for every auth call. The developers assumed that logging would be benign because "we hash sensitive data elsewhere."

## The Vulnerability

```csharp
public async Task<LoginResult> AuthenticateUser(LoginRequest request)
{
    try
    {
        // Log the raw request for debugging
        _logger.LogInformation($"Auth attempt: {JsonConvert.SerializeObject(request)}");
        
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            _logger.LogWarning($"User not found: {request.Email}");
            return LoginResult.Failed();
        }
        
        var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        _logger.LogInformation($"Password validation result: {isValid}");
        
        if (isValid)
        {
            _logger.LogInformation($"Login successful for user: {user.Email}");
            return LoginResult.Success(user);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Auth exception for {request.Email}: {ex}");
    }
}
```

Multiple PII fields leaked:
1. `request.Email` - customer email (customer identifier)
2. `request.Password` - **plaintext password** in the JSON serialization
3. `user.Email` - confirmed user existence

All of these ended up in Datadog logs, searchable and unencrypted.

## The Real-World Impact

- **Oct 2023**: Security audit discovered 18 months of plaintext passwords in logs
- **Scope**: ~45K user login attempts, including 8.2K unique passwords
- **Breach**: Contract worker with read access to Datadog had unsupervised access
- **Exposure**: Passwords available in logs for 547 days before discovery
- **Regulatory**: GDPR/CCPA violation notices, $2.1M settlement

## How GCI0029 Catches This

GCI0029 flags logging calls with PII terms that haven't been explicitly transformed:

```
Rule: GCI0029 - PII Logging Leak
  Detects: Log calls with PII terms (password, token, email, SSN, creditcard, etc.)
  Guards: Allows logs with Hash, Encrypt, Redact, Anonymize, Token (JWT) patterns
  
Finding: PII term 'password' found in log call
Location: AuthService.cs:12
Risk: Plaintext sensitive data in logs
Severity: HIGH
```

The guard clause allows legitimate encrypted/hashed values:
- `_logger.LogInformation($"Hash: {SHA256.ComputeHash(data)}")` ✅ Safe
- `_logger.LogInformation($"Token: {user.ApiToken}")` ✅ JWT tokens are expected
- `_logger.LogInformation($"Password: {request.Password}")` ❌ Flagged

## The Fix

```csharp
public async Task<LoginResult> AuthenticateUser(LoginRequest request)
{
    try
    {
        // Log sanitized info, never the raw request
        _logger.LogInformation($"Auth attempt for email domain: {GetDomain(request.Email)}");
        
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            _logger.LogWarning($"User not found (email lookup)");
            return LoginResult.Failed();
        }
        
        var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        
        // Never log password validation result
        _logger.LogInformation("Password validation completed");
        
        if (isValid)
        {
            // Log success without PII
            _logger.LogInformation($"Login successful, user_id={user.Id}");
            return LoginResult.Success(user);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Auth exception occurred: {ex.Message}");
    }
}

private string GetDomain(string email) => email.Split('@')[1];
```

Key improvements:
- **No raw passwords**: Removed plaintext password from logs
- **Email domain only**: Logs email domain, not full address
- **User ID instead of email**: Uses internal ID for user identification
- **Exception messages only**: No sensitive data in error logs

## Detection & Remediation

- **Detection**: GCI0029 flags any log call containing PII terms not guarded by transform
- **False positives**: Reduced by guard clauses that detect hashing/encryption
- **Manual review**: Developer determines if PII is necessary or if sanitization is possible
- **Automated**: Logs that use Transform patterns (Hash, Encrypt, Redact) are whitelisted

---

**Lesson**: Never log raw PII. If debugging requires sensitive data, log only hashes/IDs and use structured correlation IDs to trace requests.
