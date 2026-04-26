# Risky Diff Gallery

Real examples of code changes where small diffs can carry meaningful behavior risk.

GauntletCI focuses on the risk introduced by a change, not general code quality.

Each example shows:

- What changed
- Why the change matters
- What GauntletCI flagged
- What validation should be considered

A finding is not a claim that the code is definitely broken. It is evidence that the diff deserves review.

## Examples

| Example | Project | Risk type |
| --- | --- | --- |
| [LINQ in loop performance risk](efcore-linq-loop.md) | dotnet/efcore | Performance |
| [Null-forgiving operator on nullable return](dapper-null-forgiving.md) | DapperLib/Dapper | Null safety |
| [Shared context mutation across requests](stackexchange-redis-context-mutation.md) | StackExchange/StackExchange.Redis | Concurrency |
| [Integer overflow on size calculation](sharpcompress-overflow.md) | adamhathcock/sharpcompress | Data integrity |
| [Enum member removal from public API](anglesharp-enum-removal.md) | AngleSharp/AngleSharp | Breaking change |
