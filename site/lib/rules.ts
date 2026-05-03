import {
  AlertTriangle,
  Database,
  Cpu,
  Shield,
  Activity,
  GitBranch,
  TestTube,
  type LucideIcon,
} from "lucide-react";

export type Severity = "Block" | "Warn" | "Info";

export type Rule = {
  id: string;
  name: string;
  description: string;
  severity: Severity;
  categorySlug: CategorySlug;
  whyExists: string;
  example: {
    bad: string;
    good: string;
    language?: string;
  };
  relatedIds?: string[];
};

export type CategorySlug =
  | "behavior"
  | "security"
  | "data"
  | "concurrency"
  | "observability"
  | "architecture"
  | "quality";

export type Category = {
  slug: CategorySlug;
  title: string;
  tagline: string;
  icon: LucideIcon;
  color: string;
  badgeColor: string;
};

export const categories: Category[] = [
  {
    slug: "behavior",
    title: "Behavior and Contracts",
    tagline:
      "Logic changes, API contracts, and behavioral shifts that tests may not exercise",
    icon: AlertTriangle,
    color: "text-amber-400",
    badgeColor: "bg-amber-400/10 text-amber-400 ring-amber-400/20",
  },
  {
    slug: "security",
    title: "Security",
    tagline:
      "Credential exposure, unsafe APIs, PII leaks, and supply chain risks",
    icon: Shield,
    color: "text-red-400",
    badgeColor: "bg-red-400/10 text-red-400 ring-red-400/20",
  },
  {
    slug: "data",
    title: "Data Integrity",
    tagline:
      "Truncation risks, idempotency gaps, and unsafe data operations",
    icon: Database,
    color: "text-blue-400",
    badgeColor: "bg-blue-400/10 text-blue-400 ring-blue-400/20",
  },
  {
    slug: "concurrency",
    title: "Async and Concurrency",
    tagline:
      "Blocking calls, resource leaks, deadlock risks, and unsafe async patterns",
    icon: Cpu,
    color: "text-purple-400",
    badgeColor: "bg-purple-400/10 text-purple-400 ring-purple-400/20",
  },
  {
    slug: "observability",
    title: "Observability and Error Handling",
    tagline:
      "Swallowed exceptions, silent failures, and nullable contract violations",
    icon: Activity,
    color: "text-cyan-400",
    badgeColor: "bg-cyan-400/10 text-cyan-400 ring-cyan-400/20",
  },
  {
    slug: "architecture",
    title: "Architecture and Design",
    tagline:
      "DI anti-patterns, layer violations, complexity, and supply chain drift",
    icon: GitBranch,
    color: "text-green-400",
    badgeColor: "bg-green-400/10 text-green-400 ring-green-400/20",
  },
  {
    slug: "quality",
    title: "Code Quality and Test Gaps",
    tagline:
      "TODO stubs, test assertion gaps, and performance regressions",
    icon: TestTube,
    color: "text-orange-400",
    badgeColor: "bg-orange-400/10 text-orange-400 ring-orange-400/20",
  },
];

export const rules: Rule[] = [
  {
    id: "GCI0001",
    name: "Diff Integrity",
    severity: "Block",
    categorySlug: "behavior",
    description:
      "Detects unrelated changes, formatting churn, and mixed scope within a single diff.",
    whyExists:
      "Mixed-scope diffs hide intent. When a single commit reformats whitespace, renames variables, and fixes a bug, reviewers cannot tell which lines actually change behavior. Bugs ride into production under cover of noise.",
    example: {
      language: "csharp",
      bad: "// Same commit: reformats 200 lines AND changes a calculation\n- decimal total = price + tax;\n+ decimal total = (price + tax) * discount;\n+ // ...plus 200 lines of unrelated reformatting",
      good: "// Commit 1: reformatting only\n// Commit 2: behavioral change\n- decimal total = price + tax;\n+ decimal total = (price + tax) * discount;",
    },
    relatedIds: ["GCI0003", "GCI0046"],
  },
  {
    id: "GCI0003",
    name: "Behavioral Change Detection",
    severity: "Block",
    categorySlug: "behavior",
    description:
      "Detects removed logic lines and changed method signatures that alter runtime behavior without corresponding test updates.",
    whyExists:
      "A line removed from production code is a behavior change. If no test changed in the same diff, either the removed line was untested (silent regression risk) or the test it broke was deleted to make CI green.",
    example: {
      language: "csharp",
      bad: "// Removes a guard clause without touching tests\n- if (user is null) throw new ArgumentNullException(nameof(user));\n  return user.Email;",
      good: "// Removes the guard AND adds a test asserting the new contract\n- if (user is null) throw new ArgumentNullException(nameof(user));\n  return user.Email;\n+ // tests/UserTests.cs\n+ [Fact] public void GetEmail_NullUser_Throws_NullReference() { ... }",
    },
    relatedIds: ["GCI0004", "GCI0006", "GCI0036"],
  },
  {
    id: "GCI0004",
    name: "Breaking Change Risk",
    severity: "Block",
    categorySlug: "behavior",
    description:
      "Detects removed public APIs and changed public method signatures that may break callers.",
    whyExists:
      "Public APIs are contracts with every caller in every consuming repo. Removing or renaming one without a deprecation cycle breaks downstream builds and forces emergency releases.",
    example: {
      language: "csharp",
      bad: "- public Task<Order> GetOrder(int id)\n+ public Task<Order> GetOrderAsync(Guid id)",
      good: "  [Obsolete(\"Use GetOrderAsync(Guid). Will be removed in v3.\")]\n  public Task<Order> GetOrder(int id) => GetOrderAsync(new Guid(id.ToString()));\n+ public Task<Order> GetOrderAsync(Guid id) { ... }",
    },
    relatedIds: ["GCI0021", "GCI0047", "GCI0052"],
  },
  {
    id: "GCI0006",
    name: "Edge Case Handling",
    severity: "Warn",
    categorySlug: "behavior",
    description:
      "Detects potential null dereferences and missing validation in added code.",
    whyExists:
      "Most production NullReferenceExceptions come from added code that assumes non-null inputs without guarding for them. The cost of one guard clause is a few characters; the cost of a missing one is a 2 a.m. page.",
    example: {
      language: "csharp",
      bad: "+ public string FormatName(User user) => user.FirstName + \" \" + user.LastName;",
      good: "+ public string FormatName(User user)\n+ {\n+     ArgumentNullException.ThrowIfNull(user);\n+     return $\"{user.FirstName} {user.LastName}\";\n+ }",
    },
    relatedIds: ["GCI0003", "GCI0043", "GCI0007"],
  },
  {
    id: "GCI0007",
    name: "Error Handling Integrity",
    severity: "Block",
    categorySlug: "observability",
    description:
      "Detects swallowed exceptions (empty catch blocks) and exception handling patterns that hide failures from callers and operators.",
    whyExists:
      "An empty catch turns an actionable failure into a silent one. Operators lose the alert, callers lose the signal, and the bug compounds for hours before someone notices the dashboard is wrong.",
    example: {
      language: "csharp",
      bad: "  try { await ProcessAsync(order); }\n+ catch { }",
      good: "  try { await ProcessAsync(order); }\n+ catch (Exception ex)\n+ {\n+     _logger.LogError(ex, \"Order {OrderId} failed\", order.Id);\n+     throw;\n+ }",
    },
    relatedIds: ["GCI0032", "GCI0029", "GCI0043"],
  },
  {
    id: "GCI0010",
    name: "Hardcoding and Configuration",
    severity: "Block",
    categorySlug: "security",
    description:
      "Detects hardcoded IPs, URLs, connection strings, secrets, and environment names committed to source.",
    whyExists:
      "Secrets in source code leak through forks, mirrors, search indexes, and logs. Hardcoded environment URLs cause prod traffic to hit staging the moment a config flag flips wrong.",
    example: {
      language: "csharp",
      bad: "+ var conn = \"Server=10.0.0.5;Database=Prod;User Id=admin;Password=hunter2\";",
      good: "+ var conn = _config.GetConnectionString(\"Orders\")\n+     ?? throw new InvalidOperationException(\"Orders connection string missing\");",
    },
    relatedIds: ["GCI0012", "GCI0048"],
  },
  {
    id: "GCI0012",
    name: "Security Risk",
    severity: "Block",
    categorySlug: "security",
    description:
      "Detects SQL injection patterns, weak crypto algorithms (MD5, SHA1, DES), dangerous APIs (Assembly.Load, Process.Start), and credential exposure.",
    whyExists:
      "These are not theoretical risks. SQL injection, MD5 password hashes, and unvalidated Process.Start calls are still the top sources of breach disclosures every year.",
    example: {
      language: "csharp",
      bad: "+ var sql = $\"SELECT * FROM Users WHERE Email = '{email}'\";\n+ using var hash = MD5.Create();",
      good: "+ var sql = \"SELECT * FROM Users WHERE Email = @Email\";\n+ cmd.Parameters.AddWithValue(\"@Email\", email);\n+ var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));",
    },
    relatedIds: ["GCI0010", "GCI0048", "GCI0029"],
  },
  {
    id: "GCI0015",
    name: "Data Integrity Risk",
    severity: "Block",
    categorySlug: "data",
    description:
      "Detects unchecked casts, mass assignment without validation, and SQL ON CONFLICT IGNORE patterns that silently discard errors.",
    whyExists:
      "Silent data discard is the worst kind of bug: the system behaves correctly under tests, but production data slowly diverges from reality. By the time anyone notices, the audit trail is gone.",
    example: {
      language: "csharp",
      bad: "+ INSERT INTO orders (id, total) VALUES (@id, @total) ON CONFLICT DO NOTHING;",
      good: "+ INSERT INTO orders (id, total) VALUES (@id, @total)\n+ ON CONFLICT (id) DO UPDATE SET total = EXCLUDED.total\n+ WHERE orders.updated_at < EXCLUDED.updated_at;",
    },
    relatedIds: ["GCI0021", "GCI0022", "GCI0050"],
  },
  {
    id: "GCI0016",
    name: "Concurrency and State Risk",
    severity: "Block",
    categorySlug: "concurrency",
    description:
      "Detects async void methods, blocking async calls (.Result, .Wait()), static mutable state, and patterns that introduce deadlock risk.",
    whyExists:
      "async void cannot be awaited and crashes the process on unhandled exceptions. .Result on the request thread deadlocks under load. Static mutable state corrupts under parallel requests. All three pass unit tests and fail in production.",
    example: {
      language: "csharp",
      bad: "+ public async void HandleClick() { await SaveAsync(); }\n+ var data = httpClient.GetStringAsync(url).Result;",
      good: "+ public async Task HandleClickAsync() { await SaveAsync(); }\n+ var data = await httpClient.GetStringAsync(url);",
    },
    relatedIds: ["GCI0024", "GCI0039", "GCI0046"],
  },
  {
    id: "GCI0021",
    name: "Data and Schema Compatibility",
    severity: "Block",
    categorySlug: "behavior",
    description:
      "Detects removed serialization attributes and enum member removals that may break wire formats or persisted data.",
    whyExists:
      "Removing an enum value or [JsonPropertyName] attribute breaks every consumer that has that value persisted in a database, on a queue, or in a snapshot. The deploy succeeds; the next read fails.",
    example: {
      language: "csharp",
      bad: "  public enum OrderStatus\n  {\n      Pending,\n-     Processing,\n      Shipped\n  }",
      good: "  public enum OrderStatus\n  {\n      Pending,\n      [Obsolete(\"Use Fulfilled\")] Processing,\n      Shipped,\n+     Fulfilled,\n  }",
    },
    relatedIds: ["GCI0004", "GCI0015", "GCI0050"],
  },
  {
    id: "GCI0022",
    name: "Idempotency and Retry Safety",
    severity: "Warn",
    categorySlug: "data",
    description:
      "Detects HTTP POST endpoints without idempotency keys and raw INSERT statements without upsert guards, which are unsafe under retry logic.",
    whyExists:
      "Networks retry. Clients retry. Job runners retry. A POST that creates a duplicate row on retry is the canonical cause of double-charged customers.",
    example: {
      language: "csharp",
      bad: "+ [HttpPost(\"/orders\")]\n+ public Task<Order> Create(OrderRequest req) => _svc.CreateAsync(req);",
      good: "+ [HttpPost(\"/orders\")]\n+ public Task<Order> Create([FromHeader(Name=\"Idempotency-Key\")] Guid key, OrderRequest req)\n+     => _svc.CreateAsync(key, req);",
    },
    relatedIds: ["GCI0015", "GCI0039"],
  },
  {
    id: "GCI0024",
    name: "Resource Lifecycle",
    severity: "Warn",
    categorySlug: "concurrency",
    description:
      "Detects disposable resources allocated without a using statement or try/finally disposal, leading to connection and handle leaks.",
    whyExists:
      "An undisposed SqlConnection or FileStream eventually exhausts the pool or the OS handle table. The symptom is a slow degradation that production alerting will only catch after customer impact.",
    example: {
      language: "csharp",
      bad: "+ var conn = new SqlConnection(cs);\n+ conn.Open();\n+ var cmd = conn.CreateCommand();",
      good: "+ using var conn = new SqlConnection(cs);\n+ await conn.OpenAsync();\n+ using var cmd = conn.CreateCommand();",
    },
    relatedIds: ["GCI0016", "GCI0039"],
  },
  {
    id: "GCI0029",
    name: "PII Entity Logging Leak",
    severity: "Warn",
    categorySlug: "security",
    description:
      "Detects PII-sensitive terms (email, SSN, password, etc.) appearing inside log calls in added lines.",
    whyExists:
      "Logs end up in third-party aggregators, support tickets, and analytics pipelines. PII in a log line is PII in every downstream system, often outside the compliance perimeter.",
    example: {
      language: "csharp",
      bad: "+ _logger.LogInformation(\"Login attempt: email={Email}, password={Password}\", email, password);",
      good: "+ _logger.LogInformation(\"Login attempt: userId={UserId}\", user.Id);",
    },
    relatedIds: ["GCI0010", "GCI0012", "GCI0007"],
  },
  {
    id: "GCI0032",
    name: "Uncaught Exception Path",
    severity: "Block",
    categorySlug: "concurrency",
    description:
      "Fires when throw new is added without a corresponding Assert.Throws or Should().Throw assertion in the test suite.",
    whyExists:
      "An exception added without a test is a contract change with no safety net. Callers that relied on the old behavior break silently; future refactors cannot tell whether the throw is intentional.",
    example: {
      language: "csharp",
      bad: "+ if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));\n  // no test added",
      good: "+ if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));\n+ // tests/PaymentTests.cs\n+ [Fact] public void Charge_NegativeAmount_Throws() =>\n+     Assert.Throws<ArgumentOutOfRangeException>(() => svc.Charge(-1));",
    },
    relatedIds: ["GCI0007", "GCI0041"],
  },
  {
    id: "GCI0035",
    name: "Architecture Layer Guard",
    severity: "Warn",
    categorySlug: "architecture",
    description:
      "Checks added using directives against configured forbidden import pairs, enforcing architectural boundaries at commit time.",
    whyExists:
      "Layer rules written in a wiki get violated. Layer rules enforced at commit time stay enforced. Once a Domain class imports Microsoft.EntityFrameworkCore, the boundary is gone forever.",
    example: {
      language: "csharp",
      bad: "  // src/MyApp.Domain/Order.cs\n+ using Microsoft.EntityFrameworkCore;",
      good: "  // src/MyApp.Domain/Order.cs has no infrastructure imports.\n  // EF mappings live in src/MyApp.Infrastructure/.",
    },
    relatedIds: ["GCI0038", "GCI0045"],
  },
  {
    id: "GCI0036",
    name: "Pure Context Mutation",
    severity: "Block",
    categorySlug: "behavior",
    description:
      "Detects assignment operators inside property getter blocks or methods decorated with [Pure], indicating unexpected side effects.",
    whyExists:
      "Property getters and [Pure] methods are called by debuggers, serializers, and LINQ providers. Hidden side effects in them produce non-deterministic bugs that reproduce only under specific tooling.",
    example: {
      language: "csharp",
      bad: "+ public int Count\n+ {\n+     get { _accessCount++; return _items.Count; }\n+ }",
      good: "+ public int Count => _items.Count;\n+ public void RecordAccess() => _accessCount++;",
    },
    relatedIds: ["GCI0003", "GCI0016"],
  },
  {
    id: "GCI0038",
    name: "Dependency Injection Safety",
    severity: "Warn",
    categorySlug: "architecture",
    description:
      "Detects DI anti-patterns: service locator usage, direct instantiation of injectable types, and captive dependency violations.",
    whyExists:
      "Service locator and captive dependencies make tests harder, lifetimes unpredictable, and refactors expensive. The cost of fixing DI patterns grows superlinearly with codebase size.",
    example: {
      language: "csharp",
      bad: "+ public class OrderService\n+ {\n+     private readonly IRepo _repo = ServiceLocator.Get<IRepo>();\n+ }",
      good: "+ public class OrderService\n+ {\n+     private readonly IRepo _repo;\n+     public OrderService(IRepo repo) => _repo = repo;\n+ }",
    },
    relatedIds: ["GCI0035", "GCI0045", "GCI0046"],
  },
  {
    id: "GCI0039",
    name: "External Service Safety",
    severity: "Block",
    categorySlug: "concurrency",
    description:
      "Detects unsafe HTTP client usage and external service call patterns that lack timeout, cancellation, or retry configuration.",
    whyExists:
      "Default HttpClient timeouts are 100 seconds. A single slow downstream service can drain your entire thread pool and take the whole app offline before any health check fires.",
    example: {
      language: "csharp",
      bad: "+ var http = new HttpClient();\n+ var resp = await http.GetAsync(url);",
      good: "+ var resp = await _httpClientFactory.CreateClient(\"orders\")\n+     .GetAsync(url, ct);  // factory configures timeout, retry, circuit breaker",
    },
    relatedIds: ["GCI0024", "GCI0016", "GCI0022"],
  },
  {
    id: "GCI0041",
    name: "Test Quality Gaps",
    severity: "Warn",
    categorySlug: "quality",
    description:
      "Detects low-quality test patterns: silenced tests ([Ignore]/[Skip]), uninformative method names, and test methods missing any assertions.",
    whyExists:
      "A test with no assertions is theater: it runs, it passes, it proves nothing. A silenced test that never gets unsilenced is dead code that signals false coverage.",
    example: {
      language: "csharp",
      bad: "+ [Fact] public void Test1() { var x = svc.DoThing(); }",
      good: "+ [Fact] public void DoThing_WithValidInput_ReturnsExpectedResult()\n+ {\n+     var result = svc.DoThing(\"input\");\n+     Assert.Equal(\"expected\", result);\n+ }",
    },
    relatedIds: ["GCI0032", "GCI0042"],
  },
  {
    id: "GCI0042",
    name: "TODO and Stub Detection",
    severity: "Info",
    categorySlug: "quality",
    description:
      "Fires when added lines in non-test files contain TODO, FIXME, HACK markers, or throw new NotImplementedException, indicating unfinished work.",
    whyExists:
      "TODOs that ship to main rarely get done. NotImplementedException in production code is a deferred crash with no stack trace context. Better to merge complete or not at all.",
    example: {
      language: "csharp",
      bad: "+ public decimal CalculateTax(Order o) => throw new NotImplementedException();",
      good: "+ public decimal CalculateTax(Order o) => o.Subtotal * _taxRate.For(o.Region);",
    },
    relatedIds: ["GCI0041", "GCI0045"],
  },
  {
    id: "GCI0043",
    name: "Nullability and Type Safety",
    severity: "Info",
    categorySlug: "observability",
    description:
      "Detects null-forgiving operator (!) overuse, pragma warning disables for nullable, and unchecked as-casts that bypass the type system.",
    whyExists:
      "Every ! and #pragma warning disable nullable is a promise to the compiler that the developer knows better. When that promise is wrong, you get a NullReferenceException with no help from the type system.",
    example: {
      language: "csharp",
      bad: "+ var name = user!.Profile!.DisplayName!;",
      good: "+ var name = user?.Profile?.DisplayName\n+     ?? throw new InvalidOperationException(\"Profile missing\");",
    },
    relatedIds: ["GCI0006", "GCI0007"],
  },
  {
    id: "GCI0044",
    name: "Performance Hotpath Risk",
    severity: "Info",
    categorySlug: "quality",
    description:
      "Detects Thread.Sleep, LINQ queries inside loops, and unbounded collection growth inside loops that degrade throughput in hot paths.",
    whyExists:
      "Thread.Sleep blocks a thread-pool thread. LINQ inside a loop turns O(n) into O(n^2). Unbounded list growth turns a 100-item test into an OOM at 100k items. None of these show up in unit tests.",
    example: {
      language: "csharp",
      bad: "+ foreach (var id in ids)\n+ {\n+     var match = users.FirstOrDefault(u => u.Id == id);\n+     Thread.Sleep(10);\n+ }",
      good: "+ var byId = users.ToDictionary(u => u.Id);\n+ foreach (var id in ids)\n+ {\n+     var match = byId.GetValueOrDefault(id);\n+     await Task.Delay(10, ct);\n+ }",
    },
    relatedIds: ["GCI0016", "GCI0024"],
  },
  {
    id: "GCI0045",
    name: "Complexity Control",
    severity: "Info",
    categorySlug: "architecture",
    description:
      "Detects over-engineering: single-use interfaces, abstract classes without abstract members, and unnecessary indirection added in the diff.",
    whyExists:
      "Speculative abstraction is a tax on every future reader. An IFoo with one implementation and no test double doubles the navigation cost without adding flexibility.",
    example: {
      language: "csharp",
      bad: "+ public interface IFoo { void Bar(); }\n+ public class Foo : IFoo { public void Bar() { ... } }\n+ // IFoo has exactly one implementation and no tests use it",
      good: "+ public class Foo { public void Bar() { ... } }\n+ // Extract IFoo when the second implementation or test double appears.",
    },
    relatedIds: ["GCI0038", "GCI0046"],
  },
  {
    id: "GCI0046",
    name: "Pattern Consistency Deviation",
    severity: "Info",
    categorySlug: "architecture",
    description:
      "Detects mixed sync/async naming conventions and service locator anti-patterns introduced inconsistently within the same file.",
    whyExists:
      "Inconsistency confuses callers. A class with both GetUser and GetOrderAsync forces every caller to remember which is which, and the wrong choice can deadlock.",
    example: {
      language: "csharp",
      bad: "+ public Task<User> GetUser(int id) => ...;\n+ public Task<Order> GetOrderAsync(int id) => ...;",
      good: "+ public Task<User> GetUserAsync(int id) => ...;\n+ public Task<Order> GetOrderAsync(int id) => ...;",
    },
    relatedIds: ["GCI0016", "GCI0038", "GCI0047"],
  },
  {
    id: "GCI0047",
    name: "Naming and Contract Alignment",
    severity: "Info",
    categorySlug: "behavior",
    description:
      "Detects method renames where the new CRUD verb semantically contradicts the old verb, signaling an intent mismatch.",
    whyExists:
      "Renaming Delete to Get keeps the old behavior but advertises a new contract. Callers see Get and assume safety; the method still deletes. Rename refactors must match new behavior, not just new vocabulary.",
    example: {
      language: "csharp",
      bad: "- public void DeleteOrder(int id) { _repo.Remove(id); _repo.Save(); }\n+ public void GetOrder(int id)  { _repo.Remove(id); _repo.Save(); }",
      good: "- public void DeleteOrder(int id) { _repo.Remove(id); _repo.Save(); }\n+ public Order GetOrder(int id) => _repo.Find(id);\n+ public void DeleteOrder(int id) { _repo.Remove(id); _repo.Save(); }",
    },
    relatedIds: ["GCI0004", "GCI0046"],
  },
  {
    id: "GCI0048",
    name: "Insecure Random in Security Context",
    severity: "Info",
    categorySlug: "security",
    description:
      "Detects System.Random instantiation within 5 lines of security-sensitive identifiers such as token, apikey, salt, or password. System.Random is not cryptographically secure.",
    whyExists:
      "System.Random is a linear congruential generator with a predictable seed. Using it for tokens, salts, or password resets makes those values guessable by anyone who can observe a few outputs.",
    example: {
      language: "csharp",
      bad: "+ var rng = new Random();\n+ var token = rng.Next().ToString(\"x\");",
      good: "+ var bytes = RandomNumberGenerator.GetBytes(32);\n+ var token = Convert.ToHexString(bytes);",
    },
    relatedIds: ["GCI0010", "GCI0012"],
  },
  {
    id: "GCI0049",
    name: "Float and Double Equality Comparison",
    severity: "Info",
    categorySlug: "data",
    description:
      "Detects direct equality (== / !=) comparisons involving floating-point values, which produce unreliable results due to precision loss.",
    whyExists:
      "0.1 + 0.2 != 0.3 in IEEE 754. Equality on floats is almost always a bug waiting to surface when input distributions shift slightly.",
    example: {
      language: "csharp",
      bad: "+ if (totalRatio == 1.0) Commit();",
      good: "+ if (Math.Abs(totalRatio - 1.0) < 1e-9) Commit();\n+ // or use decimal for monetary or ratio math",
    },
    relatedIds: ["GCI0015", "GCI0050"],
  },
  {
    id: "GCI0050",
    name: "SQL Column Truncation Risk",
    severity: "Info",
    categorySlug: "data",
    description:
      "Detects short nvarchar(N) or varchar(N) column definitions that may silently truncate data when real-world values exceed the column width.",
    whyExists:
      "varchar(50) for an email is fine until the first user with a long address. Silent truncation produces data corruption that is impossible to recover after the fact.",
    example: {
      language: "sql",
      bad: "+ Email nvarchar(50) NOT NULL,",
      good: "+ Email nvarchar(320) NOT NULL,  -- RFC 5321 max",
    },
    relatedIds: ["GCI0015", "GCI0021"],
  },
  {
    id: "GCI0052",
    name: "Dependency Bot API Drift",
    severity: "Block",
    categorySlug: "architecture",
    description:
      "Fires when a dependency bot PR (Dependabot, Renovate, Snyk) contains both a lockfile change and a public API method signature change in C# files.",
    whyExists:
      "Dependency bot PRs are usually skim-reviewed. A bot PR that also changes a public method signature is either a malicious supply chain attack or an unannounced breaking upgrade. Both deserve a hard stop.",
    example: {
      language: "diff",
      bad: "  // Dependabot PR \"bump SomeLib from 1.2 to 1.3\"\n  packages.lock.json | 4 ++--\n+ src/Api/UserController.cs\n+ - public Task<User> Get(int id)\n+ + public Task<User> Get(Guid id)",
      good: "  // Dependabot PR contains only the lockfile diff.\n  packages.lock.json | 4 ++--",
    },
    relatedIds: ["GCI0004", "GCI0053"],
  },
  {
    id: "GCI0053",
    name: "Lockfile Changed Without Source Review",
    severity: "Warn",
    categorySlug: "security",
    description:
      "Fires when a diff contains only lockfile changes with no accompanying source-file edits, which can hide malicious dependency upgrades.",
    whyExists:
      "Pure lockfile PRs hide the actual supply chain change behind a one-line summary. Reviewers see the bot, click approve, and never read the transitive dependency graph that just shifted under them.",
    example: {
      language: "diff",
      bad: "  packages.lock.json | 200 +++++++++++++++++++++++++++\n  // no other files touched, no release notes linked",
      good: "  packages.lock.json | 200 +++++++++++++++++++++++++++\n+ docs/upgrades/2026-04-someLib.md  // upgrade rationale, CHANGELOG link, manual smoke notes",
    },
    relatedIds: ["GCI0052", "GCI0010"],
  },
  {
    id: "GCI0020",
    name: "Resource Exhaustion Pattern Detection",
    severity: "Block",
    categorySlug: "security",
    description:
      "Detects patterns that lead to resource exhaustion vulnerabilities: timeout removal, iteration limit removal, resource limit increases, cleanup removal, and unbounded async operations.",
    whyExists:
      "Resource exhaustion attacks rely on removing the safeguards that bound resource use. Timeouts, iteration limits, and cleanup code are the first things an attacker removes. Catching their removal stops denial-of-service attacks before deployment.",
    example: {
      language: "csharp",
      bad: "  try { await ProcessAsync(order); }\n- catch (TimeoutException) { }\n  // OR\n- using var conn = new SqlConnection(cs);",
      good: "  try { await ProcessAsync(order, TimeSpan.FromSeconds(30)); }\n+ catch (TimeoutException ex) { _logger.LogError(ex); throw; }",
    },
    relatedIds: ["GCI0024", "GCI0007"],
  },
  {
    id: "GCI0051",
    name: "Numeric Coercion Risks",
    severity: "Warn",
    categorySlug: "data",
    description:
      "Detects implicit numeric conversions that risk truncation, overflow, or loss of precision. Flags unchecked downcasts, float-to-int conversions, and assignments from large types to small types.",
    whyExists:
      "Numeric truncation is silent. An int cast from a long value that exceeds Int32.MaxValue wraps around without warning. Precision loss in float-to-int conversions causes calculations to diverge. checked{} throws on overflow, making the failure visible.",
    example: {
      language: "csharp",
      bad: "+ int size = collection.Length;  // Length is long, could truncate\n+ int ratio = (int)(totalRatio * 100);  // float to int precision loss",
      good: "+ int size = checked((int)collection.Length);  // throws on overflow\n+ decimal ratio = (decimal)totalRatio * 100;  // use decimal for precision",
    },
    relatedIds: ["GCI0049", "GCI0015"],
  },
  {
    id: "GCI0054",
    name: "Async Void Abuse",
    severity: "Warn",
    categorySlug: "concurrency",
    description:
      "Detects public async methods that return void instead of Task, which prevents callers from awaiting and catching exceptions.",
    whyExists:
      "async void is a fire-and-forget antipattern. Any exception thrown crashes the synchronization context rather than propagating to the caller. Use async void only in event handlers where Task return is impossible.",
    example: {
      language: "csharp",
      bad: "+ public async void SaveUserAsync(User user) { await _repo.SaveAsync(user); }",
      good: "+ public async Task SaveUserAsync(User user) { await _repo.SaveAsync(user); }\n+ // Only use async void for OnClick, OnChange, event handlers, etc.",
    },
    relatedIds: ["GCI0016", "GCI0007"],
  },
  {
    id: "GCI0055",
    name: "Method Signature Change Risk",
    severity: "Block",
    categorySlug: "behavior",
    description:
      "Detects breaking method signature changes: removed parameters, new required parameters without defaults, or return type changes in public methods.",
    whyExists:
      "Public method signatures are contracts. Removing a parameter, adding a required one, or changing the return type breaks every caller. These changes demand deprecation cycles, not silent breaking changes.",
    example: {
      language: "csharp",
      bad: "- public Task<User> GetUser(int id) { }\n+ public Task<User> GetUser(Guid id) { }\n+ // OR\n- public void ProcessOrder(Order order) { }\n+ public void ProcessOrder(Order order, ProcessOptions options) { }  // no default",
      good: "  [Obsolete(\"Use GetUserAsync(Guid)\")]\n  public Task<User> GetUser(int id) => GetUserAsync(new Guid(id.ToString()));\n+ public Task<User> GetUserAsync(Guid id) { }\n+ // OR\n+ public void ProcessOrder(Order order, ProcessOptions options = null) { }  // default provided",
    },
    relatedIds: ["GCI0004", "GCI0046"],
  },
];

export function getRule(id: string): Rule | undefined {
  return rules.find((r) => r.id.toLowerCase() === id.toLowerCase());
}

export function getCategory(slug: CategorySlug): Category {
  const cat = categories.find((c) => c.slug === slug);
  if (!cat) throw new Error(`Unknown category slug: ${slug}`);
  return cat;
}

export function rulesByCategory(slug: CategorySlug): Rule[] {
  return rules.filter((r) => r.categorySlug === slug);
}
