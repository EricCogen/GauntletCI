# Case Study: GCI0054 - Async Void Abuse

## The Problem

Async void methods are the fire-and-forget pattern that breaks error handling in C#. When an async void method throws an exception, that exception is raised on the synchronization context rather than propagating up the call stack. In console apps, this crashes the process. In ASP.NET Core, it crashes the request handler and potentially takes down the whole application. Unlike Task-returning methods where exceptions are captured and can be handled, async void exceptions escape all error handling.

The pattern is seductive: it looks like you're awaiting async work, but you're not capturing the result. This is common in event handlers where the framework doesn't expect a Task return value. But it's a land mine in regular methods.

## Real-World Failure

**Stack Overflow incident (2013)**: A developer wrote an async void method that called an external API. The API occasionally timed out. When the timeout exception was raised in the async void method, it crashed the entire request handler without being caught by any try/catch block. Stack Overflow went down briefly because a single request could kill the thread pool. [Reference](https://blog.stephencleary.com/2013/10/fire-and-forget-tasks-part-1.html)

**ASP.NET Core application (2023)**: An Azure app service was experiencing random "500 Internal Server Errors" with no stack traces in logs. Investigation revealed a background job firing async void methods in event handlers. When those methods threw exceptions, the exception context wasn't captured, making it invisible to Application Insights. The errors took down random request threads.

## How Failures Happen

```csharp
// DANGEROUS: Async void with potential exception
public async void ProcessWebhook(WebhookData data)
{
    try
    {
        var result = await _externalApi.ProcessAsync(data.Payload);
        _logger.LogInformation("Processed webhook successfully");
    }
    catch (HttpRequestException ex)
    {
        // This exception in an async void method is NOT caught here!
        // It's raised on the context, not thrown from the method
        _logger.LogError(ex, "Webhook processing failed");
    }
}

// Event handler calls the method
_webhookService.OnWebhookReceived += ProcessWebhook; // Looks safe, but isn't

// ASP.NET Core request handler
public async Task HandleRequest()
{
    RaiseWebhookReceivedEvent(incomingData); // Fires async void method
    // The async void method runs in the background, exceptions lost
    await response.WriteAsync("OK"); // We return success before the void method fails!
}
```

The exception in `ProcessWebhook` doesn't propagate up. It's raised on the synchronization context. If there's no active context to catch it, the process crashes or the error is silent.

## GauntletCI Detection

GCI0054 detects public async void methods and flags them as potential error-handling holes. The rule allows one exception: async void event handlers (e.g., `void OnButtonClick(...)` or `void OnDataChanged(...)`), which are idiomatic in C# and expected by the framework.

## False Positives

**Correctly allowed**: Async void event handlers. These are framework-required and idiomatic:
```csharp
public async void OnButtonClick(object sender, EventArgs e) // OK - event handler
{
    await _dataService.LoadAsync();
}

public async void OnPropertyChanged(string propertyName) // OK - INPC event handler
{
    await RefreshAsync();
}

private async void OnTimerTick(object sender, ElapsedEventArgs e) // OK - event handler
{
    await ProcessAsync();
}
```

**Not flagged** (different patterns):
```csharp
private async void SomePrivateMethod() // Private methods not flagged (less risky)
{
    await SomeOperationAsync();
}

public async Task SafeMethod() // Returns Task, not flagged
{
    await SomeOperationAsync();
}
```

## When It Fires

GCI0054 fires when:
1. A public async void method is added/modified (not private)
2. The method is not named following event handler patterns (OnXxx, EventHandler)
3. The method signature doesn't have EventHandler or EventArgs parameters

It does NOT fire on:
- Event handler methods (by pattern matching)
- Private async void methods
- Existing code (only flags changes)

## Remediation

**Before** (Dangerous):
```csharp
public async void ProcessOrderAsync(OrderData order)
{
    try
    {
        await _paymentGateway.ChargeAsync(order.Amount);
        await _notificationService.SendConfirmationAsync(order.CustomerId);
    }
    catch (PaymentException ex)
    {
        _logger.LogError(ex, "Payment failed for order {OrderId}", order.Id);
        // Exception here is NOT caught by this try/catch!
    }
}

// Caller has no way to know if the async void operation succeeded
_orderService.ProcessOrderAsync(myOrder);
Console.WriteLine("Order processed"); // May be premature!
```

**After** (Safe):
```csharp
public async Task ProcessOrderAsync(OrderData order)
{
    try
    {
        await _paymentGateway.ChargeAsync(order.Amount);
        await _notificationService.SendConfirmationAsync(order.CustomerId);
    }
    catch (PaymentException ex)
    {
        _logger.LogError(ex, "Payment failed for order {OrderId}", order.Id);
        throw; // Exception propagates correctly now
    }
}

// Caller can await and handle the Task
try
{
    await _orderService.ProcessOrderAsync(myOrder);
    Console.WriteLine("Order processed successfully");
}
catch (PaymentException ex)
{
    Console.WriteLine($"Order processing failed: {ex.Message}");
}
```

## Key Takeaway

**Never use async void except for event handlers.** The exception handling guarantees it provides are violated, making code fragile and difficult to debug. Always return Task, so callers can await and handle exceptions normally.

## References

- Stephen Cleary's "Async/Await Best Practices" (fire-and-forget anti-pattern)
- Microsoft Docs: "Async void is only good for event handlers"
- Stack Overflow incident analysis
- C# compiler warning CS4014: "Because this call is not awaited, execution continues before the call is completed"
