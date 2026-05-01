# Case Study: GCI0045 - Service Locator Anti-Pattern

## The Problem

The Service Locator pattern masks dependencies. Instead of declaring what a class needs (constructor injection), code reaches into a global "service locator" container at runtime to grab dependencies. This hides coupling, makes testing harder, violates the Dependency Inversion Principle, and creates tight coupling to the container framework.

Code that uses Service Locator looks clean at first glance but is harder to test (can't mock), harder to refactor (hidden dependencies), and harder to reason about (you don't know what a class really depends on until you read the implementation).

## Real-World Failure

**Enterprise application architecture drift (2021)**: A large financial services company migrated from Service Locator to dependency injection. In the process, they discovered 47% of their classes had Service Locator calls. Those classes couldn't be tested in isolation. During refactoring, circular dependencies and missing service registrations were discovered in production. A single "simple fix" to add a service to the locator broke 12 dependent features because no one tracked the dependency graph.

**Microservice migration chaos (2022)**: A team tried to extract a microservice from a monolith. The extracted code used Service Locator throughout. When they tried to register services differently in the new container, things broke silently. Dependencies were missing, but the code didn't error until runtime. Debugging took weeks.

## How Failures Happen

```csharp
// ANTI-PATTERN: Service Locator
public static class ServiceLocator
{
    private static Dictionary<Type, object> _services = new();

    public static void Register(Type type, object instance)
    {
        _services[type] = instance;
    }

    public static T Resolve<T>()
    {
        if (!_services.ContainsKey(typeof(T)))
            throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
        return (T)_services[typeof(T)];
    }
}

// Using Service Locator (bad)
public class OrderProcessor
{
    public void ProcessOrder(Order order)
    {
        // Dependencies are hidden in method body, not visible in constructor
        var paymentService = ServiceLocator.Resolve<IPaymentService>();
        var notificationService = ServiceLocator.Resolve<INotificationService>();
        var auditService = ServiceLocator.Resolve<IAuditService>();

        paymentService.Charge(order.Amount);
        notificationService.SendConfirmation(order.CustomerId);
        auditService.LogTransaction(order.Id);
    }
}

// Testing is now very difficult
public class OrderProcessorTests
{
    [Fact]
    public void ProcessOrder_Success()
    {
        // How do we mock the services? They're hidden inside ProcessOrder
        // Option 1: Hack the ServiceLocator to inject mocks before the call
        ServiceLocator.Register(typeof(IPaymentService), new MockPaymentService());
        // Option 2: Go back and refactor the class (but there are 100 like this!)

        var processor = new OrderProcessor();
        processor.ProcessOrder(new Order { Amount = 100 });

        // How do we assert the mock was called? We can't easily
    }
}
```

## GauntletCI Detection

GCI0045 detects Service Locator calls by looking for patterns like:
- `ServiceLocator.Resolve<T>()`
- `Container.GetInstance<T>()`
- `IServiceProvider.GetService()`
- `AutofacContainer.Resolve()`
- `CastleWindsor.Resolve()`
- Other well-known locator libraries

The rule flags these patterns in new code as signs of hidden dependencies.

## False Positives

**Not flagged** (acceptable patterns):
```csharp
// Dependency injection - OK
public class OrderProcessor
{
    private readonly IPaymentService _paymentService;

    public OrderProcessor(IPaymentService paymentService)
    {
        _paymentService = paymentService; // Declared dependency
    }

    public void ProcessOrder(Order order)
    {
        _paymentService.Charge(order.Amount);
    }
}

// Factory pattern with DI - OK
public class OrderProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public OrderProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public OrderProcessor Create()
    {
        // This is intentional: factory uses the provider as a convenience
        // But the dependency is at the factory level, transparent to callers
        return new OrderProcessor(_serviceProvider.GetRequiredService<IPaymentService>());
    }
}
```

**Flagged** (problematic patterns):
```csharp
// Service Locator call in method body - FLAGGED
public void ProcessOrder(Order order)
{
    var service = ServiceLocator.Resolve<IPaymentService>(); // Hidden dependency
}

// Static method using Service Locator - FLAGGED
public static void CompleteCheckout(int orderId)
{
    var processor = ServiceLocator.Resolve<IOrderProcessor>(); // Can't inject, can't test
    processor.Complete(orderId);
}
```

## When It Fires

GCI0045 fires when:
1. Code calls a known Service Locator method (Resolve, GetInstance, GetService, etc.)
2. The call is not in a test file
3. The call is not in a factory/container setup file

It does NOT fire on:
- Constructor-based dependency injection
- Proper factory patterns with DI
- ASP.NET Core dependency injection (IServiceProvider in middleware is OK)

## Why It's an Anti-Pattern

| Aspect | Service Locator | Dependency Injection |
|--------|-----------------|----------------------|
| **Dependencies** | Hidden in method | Visible in constructor |
| **Testing** | Hard to mock | Easy to mock |
| **Refactoring** | Breaks silently | Breaks at compile-time |
| **Dependency graph** | Opaque | Clear |
| **Performance** | Runtime lookups | Compile-time resolution |
| **Framework coupling** | Tight to locator | Loose to DI container |

## Remediation

**Before** (Service Locator):
```csharp
public class OrderProcessor
{
    private static readonly IServiceProvider _serviceProvider = GlobalServiceProvider.Instance;

    public void ProcessOrder(Order order)
    {
        var paymentService = _serviceProvider.GetService(typeof(IPaymentService));
        var notificationService = _serviceProvider.GetService(typeof(INotificationService));
        // ...
    }
}

public class OrderProcessorTests
{
    [Fact]
    public void ProcessOrder_Success()
    {
        // Impossible to inject mocks without modifying the class
        var processor = new OrderProcessor();
        // Can't verify service calls
    }
}
```

**After** (Dependency Injection):
```csharp
public class OrderProcessor
{
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;

    public OrderProcessor(IPaymentService paymentService, INotificationService notificationService)
    {
        _paymentService = paymentService;
        _notificationService = notificationService;
    }

    public void ProcessOrder(Order order)
    {
        _paymentService.Charge(order.Amount);
        _notificationService.SendConfirmation(order.CustomerId);
    }
}

public class OrderProcessorTests
{
    [Fact]
    public void ProcessOrder_Success()
    {
        var mockPaymentService = new Mock<IPaymentService>();
        var mockNotificationService = new Mock<INotificationService>();

        var processor = new OrderProcessor(mockPaymentService.Object, mockNotificationService.Object);
        processor.ProcessOrder(new Order { Amount = 100 });

        mockPaymentService.Verify(x => x.Charge(100), Times.Once);
        mockNotificationService.Verify(x => x.SendConfirmation(It.IsAny<int>()), Times.Once);
    }
}
```

## Migration Strategy

If you have existing Service Locator code:

1. **Identify all locator calls**: Search for `.Resolve`, `.GetService`, `.GetInstance`
2. **Extract to factory method**: Convert to a factory that accepts dependencies
3. **Inject factory**: Make the factory a dependency instead of the locator
4. **Eventually: remove factory**: Replace factory with direct DI

```csharp
// Step 1: Current (Service Locator)
public class OrderService
{
    public void Process(Order order)
    {
        var processor = ServiceLocator.Resolve<IOrderProcessor>();
    }
}

// Step 2: Extract factory
public class OrderService
{
    private readonly IOrderProcessorFactory _factory;

    public OrderService(IOrderProcessorFactory factory)
    {
        _factory = factory;
    }

    public void Process(Order order)
    {
        var processor = _factory.Create();
    }
}

// Step 3: Inject processor directly
public class OrderService
{
    private readonly IOrderProcessor _processor;

    public OrderService(IOrderProcessor processor)
    {
        _processor = processor;
    }

    public void Process(Order order)
    {
        _processor.Process(order); // No factory needed
    }
}
```

## Key Takeaway

**Avoid Service Locator. Use constructor-based dependency injection instead.** It makes dependencies explicit, enables easy testing, and makes code reasoning trivial. Modern frameworks (ASP.NET Core, Spring, Google Guice) make DI the default. Service Locator should only appear in legacy code migration scenarios.

## References

- Martin Fowler: "Service Locator is an anti-pattern"
- Dependency Inversion Principle (Robert Martin)
- ASP.NET Core dependency injection documentation
- Mark Seemann's "Dependency Injection in .NET"
