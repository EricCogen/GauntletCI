# GCI0010: Hardcoding and Configuration

## What it detects

Detects six patterns in added lines:

1. IP addresses embedded in code.
2. URLs starting with `http://` or `https://` inside string literals.
3. Connection strings for databases or caches.
4. Variables named after secrets (`password`, `secret`, `apikey`, `token`) assigned a string literal.
5. Known infrastructure port numbers used as numeric literals.
6. Environment names (`production`, `staging`) embedded as string values.

## Why it matters

Values hardcoded into source code cannot be changed without modifying and redeploying the application. Credentials committed to source control end up in version history permanently. Environment-specific values break deployments when the application moves between environments.

## Example diff

```diff
+private const string ConnectionString = "Server=prod-db.internal;Database=Orders;User=sa;Password=hunter2";
```

## Example finding

```text
[GCI0010] Hardcoding and Configuration
Connection string literal detected. Move to configuration or a secrets manager.
```

## What to validate

- Should this value come from an environment variable or configuration file?
- Is this value safe to commit to source control and version history?
- Will this value need to change across environments (dev, staging, production)?

## False positive notes

This rule may fire on unit tests that use placeholder or mock connection strings. It may also fire on documentation examples embedded in code comments. Suppress with a configuration entry if the value is intentional and safe.

## How to suppress

Add `.gauntletci.json` to your repository and list this rule ID in the suppression configuration. See the [Configuration Reference](../DEVELOPMENT.md) for syntax.

## Status

Stable
