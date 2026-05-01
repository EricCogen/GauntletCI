# GCI0039 Case Study: Thread Pool Exhaustion via Uncanceled HTTP Calls

## The Problem

A backend service needed to call multiple external APIs (Stripe, AWS, Datadog) during request processing. The developers used a pattern that seemed reasonable: create an HttpClient for each external service and make calls without explicit timeout handling, trusting the framework defaults.

## The Vulnerability

```csharp
public class PaymentProcessor
{
    private readonly HttpClient _stripeClient = new HttpClient();
    private readonly HttpClient _analyticsClient = new HttpClient();
    
    public async Task ProcessOrder(Order order)
    {
        try
        {
            // Call Stripe without CancellationToken
            var charge = await _stripeClient.PostAsync(
                "https://api.stripe.com/v1/charges",
                new StringContent(JsonConvert.SerializeObject(order))
            );
            
            // Call analytics without CancellationToken
            var response = await _analyticsClient.PostAsync(
                "https://analytics.service.com/event",
                new StringContent(JsonConvert.SerializeObject(new { OrderId = order.Id }))
            );
            
            return charge.Content.ReadAsStringAsync().Result;
        }
        catch (HttpRequestException ex)
        {
            // No timeout handling - request hangs indefinitely
            _logger.LogError($"API call failed: {ex}");
            throw;
        }
    }
}
```

Problems:
1. **No CancellationToken**: Request can hang indefinitely if external service is slow
2. **Direct HttpClient instantiation**: Each service gets its own HttpClient (socket exhaustion)
3. **.Result blocking**: Blocks thread waiting for response (prevents ThreadPool scaling)
4. **No timeout defaults**: Requests wait for OS TCP timeout (30+ seconds)

## The Real-World Impact

- **Dec 2023**: Datadog API experienced outage (known issue, 45-min recovery)
- **Service behavior**: All requests started hanging on analytics calls (no CancellationToken)
- **Thread pool**: ASP.NET Core thread pool exhausted waiting for timeouts
- **Cascading failure**: Service became unresponsive, downstream services queued up requests
- **Incident**: 22 minutes of 100% error rate, affecting 8 dependent services
- **Loss**: $47K in transaction revenue, 4 hours incident response

## How GCI0039 Catches This

GCI0039 flags HTTP calls that lack CancellationToken and aren't managed by IHttpClientFactory:

```
Rule: GCI0039 - External Service Safety
  Detects: HTTP calls missing CancellationToken parameter
  Guards: Allows calls via IHttpClientFactory, Polly, or dependency-injected clients
  
Finding: Direct HttpClient instantiation without CancellationToken
Location: PaymentProcessor.cs:8
Risk: Thread pool exhaustion, cascading timeout failures
Severity: HIGH

Finding: HTTP call missing CancellationToken
Location: PaymentProcessor.cs:15
Risk: Hanging requests if external service degrades
Severity: HIGH
```

The guard clauses allow safe patterns:
- `IHttpClientFactory.CreateClient()` ✅ Framework manages lifetime, defaults
- `_httpClient.PostAsync(..., cancellationToken)` ✅ Explicit timeout
- `_client.GetAsync(url)` ✅ Injected client with defaults
- `new HttpClient().PostAsync(...)` ❌ Flagged

## The Fix

```csharp
public class PaymentProcessor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaymentProcessor> _logger;
    
    public PaymentProcessor(IHttpClientFactory httpClientFactory, ILogger<PaymentProcessor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    
    public async Task ProcessOrder(Order order, CancellationToken cancellationToken)
    {
        try
        {
            // Use IHttpClientFactory with named client
            var stripeClient = _httpClientFactory.CreateClient("stripe");
            
            // Post with explicit timeout via CancellationToken
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10s timeout for Stripe
            
            var charge = await stripeClient.PostAsync(
                "https://api.stripe.com/v1/charges",
                new StringContent(JsonConvert.SerializeObject(order)),
                cts.Token
            );
            
            // Call analytics with separate timeout
            var analyticsClient = _httpClientFactory.CreateClient("analytics");
            using var analyticsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            analyticsCts.CancelAfter(TimeSpan.FromSeconds(5)); // 5s timeout for analytics
            
            var analyticsResponse = await analyticsClient.PostAsync(
                "https://analytics.service.com/event",
                new StringContent(JsonConvert.SerializeObject(new { OrderId = order.Id })),
                analyticsCts.Token
            );
            
            return await charge.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning($"External API call timed out: {ex.Message}");
            throw new TimeoutException("Payment processing timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"API call failed: {ex}");
            throw;
        }
    }
}

// In Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddHttpClient("stripe")
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10))
        .AddPolicyHandler(GetRetryPolicy()); // Add Polly for resilience
    
    services.AddHttpClient("analytics")
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(5));
}

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
```

Key improvements:
- **IHttpClientFactory**: Framework-managed client reuse (socket pooling)
- **CancellationToken**: Explicit per-request timeout
- **LinkedTokenSource**: Honors parent request cancellation
- **Separate timeouts**: Different limits for different services (Stripe can be slower)
- **Polly integration**: Automatic retry with exponential backoff
- **No blocking**: Uses await instead of .Result

## Detection & Remediation

- **Detection**: GCI0039 flags any HTTP call without CancellationToken
- **Guards**: Recognizes IHttpClientFactory, injected clients (_httpClient, _client prefixes)
- **False positives**: Guards avoid flagging framework-managed patterns
- **Automated**: Developers can see the pattern and add CancellationToken immediately

---

**Lesson**: External APIs are unreliable. Always use CancellationToken for timeout control, and manage HttpClient lifetime via IHttpClientFactory to prevent socket exhaustion.
