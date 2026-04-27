#!/usr/bin/env python3
"""
print_survival_summary.py

Reads survival-summary.json produced by the JohnnyLike survival-study command,
prints a readable fixed-width table to stdout, and appends a Markdown table to
$GITHUB_STEP_SUMMARY (when that environment variable is set).

Usage:
    python .github/scripts/print_survival_summary.py <path-to-survival-summary.json>
"""

import json
import os
import sys


def load_summary(path: str) -> dict:
    if not os.path.isfile(path):
        print(f"ERROR: summary file not found: {path}", file=sys.stderr)
        sys.exit(1)
    with open(path, encoding="utf-8") as f:
        try:
            return json.load(f)
        except json.JSONDecodeError as exc:
            print(f"ERROR: malformed JSON in {path}: {exc}", file=sys.stderr)
            sys.exit(1)


def get(d: dict, *keys):
    """Get a value from a dict trying both the given key and common case variants."""
    for key in keys:
        if key in d:
            return d[key]
        # Try camelCase -> PascalCase conversion
        pascal = key[0].upper() + key[1:] if key else key
        if pascal in d:
            return d[pascal]
    return None


def sort_archetypes(entries: list[dict]) -> list[dict]:
    return sorted(
        entries,
        key=lambda e: (
            -(get(e, "survivedToEndRate", "SurvivedToEndRate") or 0.0),
            -(get(e, "medianSurvivalTimeSeconds", "MedianSurvivalTimeSeconds") or 0.0),
            -(get(e, "meanSurvivalTimeSeconds", "MeanSurvivalTimeSeconds") or 0.0),
        ),
    )


def fmt_pct(v: float) -> str:
    return f"{v * 100.0:.1f}%"


def fmt_days(v) -> str:
    if v is None:
        return "—"
    return f"{float(v) / 86400.0:.2f} Days"


def _g(entry: dict, *keys):
    return get(entry, *keys) or 0.0


def print_table(summary: dict, entries: list[dict]) -> None:
    duration = get(summary, "configuredDurationSeconds", "ConfiguredDurationSeconds") or 0.0
    runs     = get(summary, "runsPerActor", "RunsPerActor") or "?"
    dur_days = fmt_days(duration)

    print("=== ARCHETYPE SURVIVAL SUMMARY ===")
    print(f"Duration: {dur_days}")
    print(f"Runs per archetype: {runs}")
    print()

    # Column widths
    actor_w = max(10, max((len(get(e, "actor", "Actor") or "") for e in entries), default=10))
    time_w  = 12
    header = (
        f"{'Rank':>4}  {'Actor':<{actor_w}}  {'Survive%':>9}  "
        f"{'Mean':>{time_w}}  {'Median':>{time_w}}  {'StdDev':>{time_w}}  "
        f"{'Min':>{time_w}}  {'Max':>{time_w}}"
    )
    sep = (
        f"{'----':>4}  {'-' * actor_w:<{actor_w}}  {'---------':>9}  "
        f"{'----------':>{time_w}}  {'----------':>{time_w}}  {'----------':>{time_w}}  "
        f"{'----------':>{time_w}}  {'----------':>{time_w}}"
    )

    print(header)
    print(sep)

    for rank, e in enumerate(entries, start=1):
        actor  = get(e, "actor", "Actor") or "?"
        surv   = fmt_pct(_g(e, "survivedToEndRate", "SurvivedToEndRate"))
        mean   = fmt_days(get(e, "meanSurvivalTimeSeconds", "MeanSurvivalTimeSeconds"))
        median = fmt_days(get(e, "medianSurvivalTimeSeconds", "MedianSurvivalTimeSeconds"))
        stddev = fmt_days(get(e, "stddevSurvivalTimeSeconds", "StddevSurvivalTimeSeconds"))
        mn     = fmt_days(get(e, "minSurvivalTimeSeconds", "MinSurvivalTimeSeconds"))
        mx     = fmt_days(get(e, "maxSurvivalTimeSeconds", "MaxSurvivalTimeSeconds"))

        print(
            f"{rank:>4}  {actor:<{actor_w}}  {surv:>9}  "
            f"{mean:>{time_w}}  {median:>{time_w}}  {stddev:>{time_w}}  "
            f"{mn:>{time_w}}  {mx:>{time_w}}"
        )

    print()


def write_step_summary(summary: dict, entries: list[dict]) -> None:
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return

    duration = get(summary, "configuredDurationSeconds", "ConfiguredDurationSeconds") or 0.0
    runs     = get(summary, "runsPerActor", "RunsPerActor") or "?"
    ts       = get(summary, "timestamp", "Timestamp") or ""

    lines = []
    lines.append("## 🏝️ Archetype Survival Study")
    lines.append("")
    lines.append(f"**Duration:** {fmt_days(duration)} &nbsp; **Runs per archetype:** {runs}")
    if ts:
        lines.append(f"**Generated:** {ts}")
    lines.append("")
    lines.append(
        "| Rank | Actor | Survive% | Mean | Median | StdDev | Min | Max |"
    )
    lines.append(
        "|-----:|-------|--------:|--------:|----------:|-----------:|--------:|--------:|"
    )

    for rank, e in enumerate(entries, start=1):
        actor  = get(e, "actor", "Actor") or "?"
        surv   = fmt_pct(_g(e, "survivedToEndRate", "SurvivedToEndRate"))
        mean   = fmt_days(get(e, "meanSurvivalTimeSeconds", "MeanSurvivalTimeSeconds"))
        median = fmt_days(get(e, "medianSurvivalTimeSeconds", "MedianSurvivalTimeSeconds"))
        stddev = fmt_days(get(e, "stddevSurvivalTimeSeconds", "StddevSurvivalTimeSeconds"))
        mn     = fmt_days(get(e, "minSurvivalTimeSeconds", "MinSurvivalTimeSeconds"))
        mx     = fmt_days(get(e, "maxSurvivalTimeSeconds", "MaxSurvivalTimeSeconds"))
        lines.append(f"| {rank} | {actor} | {surv} | {mean} | {median} | {stddev} | {mn} | {mx} |")

    lines.append("")

    with open(summary_path, "a", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main() -> None:
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} <survival-summary.json>", file=sys.stderr)
        sys.exit(1)

    summary = load_summary(sys.argv[1])

    raw_entries = get(summary, "archetypes", "Archetypes")
    if not isinstance(raw_entries, list) or len(raw_entries) == 0:
        print("ERROR: 'archetypes' array is missing or empty in summary JSON.", file=sys.stderr)
        sys.exit(1)

    entries = sort_archetypes(raw_entries)

    print_table(summary, entries)
    write_step_summary(summary, entries)


if __name__ == "__main__":
    main()
