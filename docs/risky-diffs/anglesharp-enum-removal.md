# Example: Enum member removal from public API

## Project

AngleSharp/AngleSharp

## What changed

A member was removed from a public enumeration type.

## Why it matters

Removing an enum member is a binary breaking change. Any code compiled against the old version that references the removed member will fail to compile or throw a runtime exception. Serialized data that stored the old integer or string value will fail to deserialize.

## Example shape

```diff
 public enum NodeType
 {
     Element = 1,
     Text = 2,
-    Comment = 3,
     Document = 4,
 }
```

## GauntletCI signal

Breaking change risk flagged by GCI0004 (Breaking Change Risk) and data schema compatibility risk flagged by GCI0021 (Data and Schema Compatibility).

## Validation to consider

- Are there callers outside this repository that reference the removed member?
- Are there stored or serialized values (in databases, files, or message queues) that map to the removed integer?
- Should the member be marked obsolete before removal in a future major version?
- Does the version number reflect a breaking change?

## Notes

Keep this page factual. Link to public PRs only when the example is public and safe to reference.
