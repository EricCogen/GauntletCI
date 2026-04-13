# SPDX-License-Identifier: Elastic-2.0
from __future__ import annotations

import json
import re


def _ev_str(evidence) -> str:
    if evidence is None:
        return ""
    if isinstance(evidence, str):
        try:
            evidence = json.loads(evidence)
        except Exception:
            return evidence
    return json.dumps(evidence, ensure_ascii=False)


def _line_ref(ev: str) -> str | None:
    m = re.search(r"Line (\d+):", ev)
    return f"line {m.group(1)}" if m else None


def _file_ref(text: str) -> str | None:
    m = re.search(
        r"in ([\w/\\. -]+\.\w{1,5})",
        text,
        re.IGNORECASE,
    )
    return m.group(1).strip() if m else None


def _loc(message: str, ev: str) -> str | None:
    lr = _line_ref(ev)
    fr = _file_ref(message)
    if fr and lr:
        return f"in `{fr}` at {lr}"
    if fr:
        return f"in `{fr}`"
    if lr:
        return f"at {lr}"
    return None


def _line_code(ev: str) -> str | None:
    m = re.search(r"Line \d+: (.+)", ev)
    return m.group(1).strip()[:120] if m else None


def generate_item_suggestion(rule_id: str, message: str, evidence) -> str | None:
    """
    Return a specific, actionable suggestion for this particular finding.
    Returns None if no specific template matches — caller should fall back to rule_def.action.
    """
    ev = _ev_str(evidence)
    loc = _loc(message, ev)
    loc_str = f" {loc}" if loc else ""

    if rule_id == "GCI0001":
        m = re.search(r"Non-code files in diff: (.+)", ev)
        if m:
            files = m.group(1).strip()
            return (
                f"This PR mixes code changes with non-code files ({files}). "
                "Consider splitting into separate PRs — one for the logic change and one for the "
                "project/config file update so each is easier to review and revert independently."
            )

    elif rule_id == "GCI0002":
        cats = re.findall(r"(Frontend|Backend|Config|Tests|Infra): True", ev, re.IGNORECASE)
        if cats:
            layers = ", ".join(cats)
            return (
                f"This PR touches {layers} layers simultaneously. "
                "Split into focused PRs — e.g., one for backend logic and a separate one for "
                "config or infrastructure changes — to reduce review complexity and merge-conflict risk."
            )

    elif rule_id == "GCI0003":
        n_m = re.search(r"(\d+) logic line", message)
        snip = re.search(r"Removed logic: (.+)", ev)
        n = n_m.group(1) if n_m else "Several"
        hint = f" (e.g., `{snip.group(1).strip()[:60]}`)" if snip else ""
        return (
            f"{n} logic line(s) removed{hint} without corresponding test changes. "
            "Add or update tests that cover the removed branches so regressions are caught before merge."
        )

    elif rule_id == "GCI0004":
        name_m = re.search(r"Public API removed: '(.+?)'", message)
        removed_m = re.search(r"Removed: (.+)", ev)
        member = f"`{name_m.group(1)}`" if name_m else "the removed member"
        snippet = f"\n\nRemoved signature: `{removed_m.group(1).strip()[:100]}`" if removed_m else ""
        return (
            f"{member}{loc_str} is a public API that was deleted. "
            "Search for all callers before merging, or use `[Obsolete]` to deprecate it first."
            f"{snippet}"
        )

    elif rule_id == "GCI0005":
        files_m = re.search(r"Changed code files: (.+)", ev)
        sample = files_m.group(1).strip()[:100] if files_m else "the changed files"
        return (
            f"No test file was updated alongside `{sample}` (and possibly others). "
            "Add unit or integration tests that exercise the changed behaviour before merging."
        )

    elif rule_id == "GCI0006":
        sig_m = re.search(r"(?:private|public|protected|internal|static)\s+\S+\s+\w+\([^)]+\)", ev)
        sig = f" (`{sig_m.group(0).strip()[:80]}`)" if sig_m else ""
        return (
            f"The new method{sig}{loc_str} accepts parameters without null or range validation. "
            "Add guard clauses at the top of the method (e.g., `ArgumentNullException.ThrowIfNull(param)`)."
        )

    elif rule_id == "GCI0007":
        return (
            f"An exception is swallowed{loc_str} — it is caught but not logged or rethrown. "
            "Either rethrow, log with context, or explicitly comment why swallowing is intentional."
        )

    elif rule_id == "GCI0008":
        dup_m = re.search(r"Duplicated: (.+)", ev)
        snippet = f" (`{dup_m.group(1).strip()[:60]}`)" if dup_m else ""
        return (
            f"The line{snippet} appears 3+ times in the added code. "
            "Extract it into a shared constant, extension method, or helper to centralise the definition."
        )

    elif rule_id == "GCI0010":
        ip_m = re.search(r"(\d+\.\d+\.\d+\.\d+)", ev + message)
        ip = f"`{ip_m.group(1)}`" if ip_m else "the IP address"
        code = _line_code(ev)
        hint = f"\n\nFlagged: `{code}`" if code else ""
        return (
            f"{ip}{loc_str} is hardcoded. Move it to an environment variable or configuration "
            f"value so it can change per environment without a code change.{hint}"
        )

    elif rule_id == "GCI0011":
        code = _line_code(ev)
        hint = f"\n\nFlagged: `{code}`" if code else ""
        return (
            f"Replace `.Count() > 0`{loc_str} with `.Any()`. "
            f"`.Count()` enumerates the entire collection; `.Any()` stops at the first element.{hint}"
        )

    elif rule_id == "GCI0012":
        kw_m = re.search(r"Possible hardcoded credential \('(.+?)'\)", message)
        kw = f"`{kw_m.group(1)}`" if kw_m else "a credential"
        code = _line_code(ev)
        code_hint = f"\n\nFlagged line: `{code}`" if code else ""
        return (
            f"The identifier {kw}{loc_str} was flagged as a possible secret. "
            "If this reads from configuration/environment at runtime it is likely a false positive — label No. "
            f"If the value is a literal string, move it to a secrets manager or environment variable immediately.{code_hint}"
        )

    elif rule_id == "GCI0013":
        return f"Add an XML `<summary>` doc comment above the public member{loc_str}."

    elif rule_id == "GCI0014":
        api_m = re.search(r"Destructive API call: (.+)", message)
        api = f"`{api_m.group(1).strip()}`" if api_m else "this call"
        code = _line_code(ev)
        hint = f"\n\nFlagged: `{code}`" if code else ""
        return (
            f"{api}{loc_str} is irreversible. "
            f"Confirm intent, add a guard check, and ensure failure paths leave state consistent.{hint}"
        )

    elif rule_id == "GCI0015":
        n_m = re.search(r"(\d+) assignments", message)
        n = n_m.group(1) if n_m else "Multiple"
        return (
            f"{n} field assignments{loc_str} occur without null validation. "
            "Add null guards or use a validated DTO / constructor pattern before assigning."
        )

    elif rule_id == "GCI0016":
        code = _line_code(ev)
        field_str = f" (`{code}`)" if code else ""
        return (
            f"The static mutable field{field_str} is shared across all threads. "
            "Make it immutable, use `ThreadLocal<T>`, or add a lock around all reads and writes."
        )

    elif rule_id == "GCI0017":
        dirs_m = re.search(r"Directories: (.+)", ev)
        dirs = dirs_m.group(1).strip() if dirs_m else "multiple top-level directories"
        return (
            f"Changes span `{dirs}`. "
            "Consider splitting into focused PRs aligned to a single module or concern."
        )

    elif rule_id == "GCI0018":
        return (
            f"A TODO/FIXME comment was left in the added code{loc_str}. "
            "Either resolve it before merging or file a tracked issue and link to it in the comment."
        )

    elif rule_id == "GCI0019":
        return (
            f"An async method{loc_str} does not propagate `CancellationToken`. "
            "Pass the token through to all awaited calls so the operation can be cancelled promptly."
        )

    elif rule_id == "GCI0020":
        return (
            f"Magic numbers appear in the added code{loc_str}. "
            "Extract them into named constants so their intent is clear and they can be changed in one place."
        )

    elif rule_id == "GCI0021":
        code = _line_code(ev)
        hint = f"\n\nFlagged: `{code}`" if code else ""
        return (
            f"String formatting is used inside a logging call{loc_str}, which eagerly allocates even when the "
            f"log level is disabled. Use structured logging parameters instead (e.g., `Log.Warning(\"Value: {{V}}\", v)`).{hint}"
        )

    elif rule_id == "GCI0022":
        return (
            f"A raw `string` is used for a value that should be a strongly-typed identifier{loc_str}. "
            "Introduce a value object or `record struct` wrapper to prevent mixing unrelated IDs."
        )

    elif rule_id == "GCI0029":
        code = _line_code(ev)
        hint = f"\n\nFlagged: `{code}`" if code else ""
        return (
            f"A PII-related identifier (e.g., email, SSN, phone) is logged{loc_str}. "
            f"Remove or mask the field before logging to comply with GDPR / CCPA requirements.{hint}"
        )

    elif rule_id == "GCI0036":
        return (
            f"ASP.NET Core `HttpContext` (or a derived property) is captured in a field or closure{loc_str}. "
            "`HttpContext` is request-scoped — accessing it outside the request lifetime causes exceptions or data leaks. "
            "Pass needed values explicitly instead."
        )

    # Generic fallback: extract file+line and prepend to nothing (caller uses rule_def.action)
    if loc:
        return None  # loc info is already shown separately — let generic action render

    return None
