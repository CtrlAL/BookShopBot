#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Deterministic TDD loop for Agent-Driven Development (ADD).

Reads a task spec from `.tasks/current_task.md`, invokes a CLI agent,
and validates its output through a fixed, ordered pipeline:
1. dotnet format --verify-no-changes   (linter / formatting)
    2. dotnet build  -p:WarningsAsErrors=CS*   (static typing / compile)
    3. dotnet test   ...                    (tests)

On failure, the last 2000 chars of the failing stage's output are fed
back verbatim into the next agent prompt. The loop runs at most N
iterations (default 5: 1 attempt + 4 self-heal retries). It exits with
code 0 ONLY when every stage passes on the final attempt, and with
code 1 when the self-heal limit is exhausted (circuit breaker: control
returns to a human).

Python 3.10+. Standard library only, no pip dependencies.

Usage:
    python scripts/tdd_loop.py [--task .tasks/current_task.md]
                               [--agent-cmd 'opencode run {prompt}']
                               [--iterations 5] [--self-heal 4]
                               [--no-baseline] [--no-require-tests] [--keep]
"""

from __future__ import annotations

import argparse
import os
import shlex
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_TASK = ROOT / ".tasks" / "current_task.md"

STACKTRACE_TAIL = 2000          # chars of failure output fed back to the agent
DEFAULT_ITERATIONS = 5          # max agent invocations in total
DEFAULT_SELF_HEAL = 4           # circuit breaker: max fix attempts
DEFAULT_AGENT_CMD = "opencode run {prompt}"

# Ordered, deterministic validation stages for the repo's stack (C# / .NET).
# Stage name -> key -> argv (run with cwd = repo root).
SLN = "BookShop/BookShop.sln"
VALIDATION_STEPS: list[tuple[str, str, list[str]]] = [
    ("LINTER / FORMAT", "format",
     ["dotnet", "format", SLN, "--verify-no-changes", "--verbosity", "diagnostic"]),
    ("TYPE CHECK / BUILD", "build",
     ["dotnet", "build", SLN, "--no-restore", "-p:WarningsAsErrors=CS*"]),
    ("TESTS", "test",
     ["dotnet", "test", SLN, "--no-build", "--verbosity", "normal"]),
]

TEST_MARKERS = (
    "Microsoft.NET.Test.Sdk",
    "NUnit3TestAdapter",
    "xunit",
    "MSTest.TestAdapter",
)

BASE_PROMPT = """\
You are an autonomous implementation agent executing a task under a strict
Agent-Driven Development (ADD) protocol. The rules below are NON-NEGOTIABLE.
You will be validated deterministically after your run.

RULES
1. TDD INVARIANT
   Do NOT modify implementation code (files under src/, BookShop/, ChatFSM/,
   TelegramBot/) before a FAILING test that demonstrates the required
   behaviour exists under tests/. Write the test first, watch it fail,
   then implement.
2. SCOPE GUARD
   Only edit files listed in the task's "Affected Files" white-list. Touching
   any other file is a protocol violation. No new NuGet/assembly references
   unless the spec explicitly allows them.
3. NON-INTERACTIVE RULE
   Do not ask the user any questions. Resolve every ambiguity yourself from
   the spec. If a decision is genuinely unmakable, leave the work as-is,
   report it in your final message, and stop.
4. CIRCUIT BREAKER
   You have a limited number of self-healing attempts. Make your first pass
   as correct as possible. When validation reports an error, fix precisely
   the reported issue; do not restructure unrelated code.

TASK SPECIFICATION
------------------
{spec}

EXIT CONTRACT
-------------
Finish your final message with the single line:
=== VERDICT: DONE ===
If that line is missing, your run is treated as incomplete.
"""

FEEDBACK_BLOCK = """\
FEEDBACK FROM PREVIOUS ATTEMPT ({failed}/{max_total})
----------------------------------------------------
Your previous run was validated and FAILED.

  Failed stage : {stage}
  Command      : {cmd}
  Attempt      : {attempt}

Output tail (last {tail_len} characters):
```
{tail}
```
----------------------------------------------------------------------
Fix exactly what the output above reports. Reuse already-working changes;
do NOT revert correct code. Re-run the verification flow yourself before
finishing, then confirm with the EXIT CONTRACT line.
"""


# --------------------------------------------------------------------------- #
# Helpers
# --------------------------------------------------------------------------- #

def combine_output(proc: subprocess.CompletedProcess) -> str:
    """Merge stdout+stderr deterministically, tolerate non-UTF8 bytes."""
    chunks: list[str] = []
    for stream in (proc.stdout, proc.stderr):
        if stream:
            if isinstance(stream, bytes):
                stream = stream.decode("utf-8", errors="replace")
            chunks.append(str(stream))
    return "\n".join(chunks).rstrip()


def run_cmd(argv: list[str], cwd: Path, timeout_sec: int) -> tuple[int, str]:
    """Run a command, capturing merged output. Returns (returncode, output)."""
    try:
        proc = subprocess.run(
            argv,
            cwd=str(cwd),
            capture_output=True,
            text=True,
            errors="replace",
            timeout=timeout_sec,
        )
    except FileNotFoundError:
        return 127, f"Command not found: '{argv[0]}'. Ensure it is installed and on PATH."
    except subprocess.TimeoutExpired as exc:
        tail = ""
        if exc.stdout:
            tail = (exc.stdout.decode("utf-8", errors="replace") if isinstance(exc.stdout, bytes)
                    else str(exc.stdout))
        if exc.stderr:
            tail += "\n" + (exc.stderr.decode("utf-8", errors="replace")
                            if isinstance(exc.stderr, bytes) else str(exc.stderr))
        return 124, f"Command timed out after {timeout_sec}s.\n{tail[:4000]}"
    return proc.returncode, combine_output(proc)


def find_test_projects(root: Path) -> list[Path]:
    """Find *.csproj that reference a test SDK (xunit/NUnit/MSTest)."""
    found: list[Path] = []
    for proj in root.rglob("*.csproj"):
        try:
            text = proj.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        if any(marker in text for marker in TEST_MARKERS):
            found.append(proj)
    return found


def parse_agent_cmd(template: str) -> list[str]:
    """Parse the agent command template into argv.

    Quotes handled via shlex so templates like ``opencode run "{prompt}"``
    work on both POSIX and Windows shells.
    """
    return shlex.split(template)


def build_agent_argv(agent_argv: list[str], prompt: str) -> list[str]:
    """Substitute the prompt into ``{prompt}`` placeholders, or append it."""
    if any("{prompt}" in part for part in agent_argv):
        return [part.replace("{prompt}", prompt) for part in agent_argv]
    return agent_argv + [prompt]


def repo_state(cwd: Path) -> str:
    """Stable snapshot of the working tree (tracked diffs + untracked paths) to
    detect no-op agent runs. Ignores mtime/content-bytes differences."""
    rc, out = run_cmd(["git", "status", "--porcelain"], cwd, 60)
    if rc != 0:
        return ""
    lines = sorted(out.splitlines())
    return "\n".join(lines)


# --------------------------------------------------------------------------- #
# Validation pipeline
# --------------------------------------------------------------------------- #

def validate(cwd: Path, require_tests: bool, timeout_sec: int) -> tuple[bool, str, str, str]:
    """Run every stage in order. Returns (all_passed, failed_stage_name,
    failed_stage_key, failure_output_tail)."""
    test_projects = find_test_projects(cwd)
    if require_tests and not test_projects:
        return (False, "TESTS", "test",
                "TDD invariant violated: no test project found under tests/ or any *.csproj "
                "referencing xunit / NUnit / MSTest. Create a failing test FIRST.\nTest markers "
                "looked for: " + ", ".join(TEST_MARKERS))

    for stage_name, stage_key, argv in VALIDATION_STEPS:
        print(f"\n-- validation: {stage_name}: {' '.join(argv)}", flush=True)
        rc, output = run_cmd(argv, cwd, timeout_sec)
        tail = output[-STACKTRACE_TAIL:]
        if rc != 0:
            print(f"-- validation FAILED ({stage_name}), exit={rc}", flush=True)
            return False, stage_name, stage_key, tail
        print(f"-- validation OK ({stage_name})", flush=True)
    return True, "", "", ""


# --------------------------------------------------------------------------- #
# Orchestration
# --------------------------------------------------------------------------- #

def compose_prompt(attempt: int, spec: str, max_total: int,
                   feedback: str | None, baseline_fail: str | None) -> str:
    prompt = BASE_PROMPT.format(spec=spec)
    if baseline_fail and attempt == 1:
        prompt += ("\n\nCURRENT STATE OF THE REPO (baseline validation failed before your run "
                   "— start your work from this state):\n```\n" + baseline_fail + "\n```")
    if feedback:
        prompt += "\n\n" + feedback
    return prompt


def print_circuit_breaker(attempt: int, failed: int, stage: str, tail: str) -> None:
    report = (
        "\n\n[CIRCUIT BREAKER] Self-healing limit reached after %d failed attempt(s).\n"
        "  Last failed stage : %s\n"
        "  Output tail       :\n```\n%s\n```\n"
        "  Attempted fixes   : %d\n"
        "  Action required   : manual intervention. Fix the repo by hand, then re-run.\n"
    ) % (failed, stage, tail, attempt)
    print(report, file=sys.stderr, flush=True)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Deterministic TDD loop: agent + lint/type-check + tests.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--task", default=str(DEFAULT_TASK),
                        help="Task spec file (default: .tasks/current_task.md)")
    parser.add_argument("--agent-cmd", default=os.environ.get("ADD_AGENT_CMD", DEFAULT_AGENT_CMD),
                        help="CLI agent command template. '{prompt}' = prompt text "
                             "(default: 'opencode run {prompt}'; env ADD_AGENT_CMD)")
    parser.add_argument("--iterations", type=int, default=int(os.environ.get("ADD_ITERATIONS", DEFAULT_ITERATIONS)),
                        help="Max agent invocations total (default 5)")
    parser.add_argument("--self-heal", type=int, default=int(os.environ.get("ADD_SELF_HEAL", DEFAULT_SELF_HEAL)),
                        help="Circuit breaker: max fix attempts (default 4)")
    parser.add_argument("--timeout", type=int, default=int(os.environ.get("ADD_CMD_TIMEOUT", "900")),
                        help="Per-command timeout in seconds (default 900)")
    parser.add_argument("--no-baseline", action="store_true",
                        help="Skip the pre-run baseline validation")
    parser.add_argument("--no-require-tests", action="store_true",
                        help="Do not require a test project to exist (weaker TDD enforcement)")
    parser.add_argument("--allow-noop", action="store_true",
                        help="Accept an agent run that changed nothing on disk "
                             "(default: a no-op is treated as a failed attempt)")
    parser.add_argument("--keep", action="store_true",
                        help="Keep generated prompt files under .tasks/.tmp/ for debugging")
    parser.add_argument("--root", default=str(ROOT),
                        help="Repo root (default: parent of this script)")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    task_path = Path(args.task).resolve()
    if not task_path.is_file():
        print(f"ERROR: task spec not found: {task_path}\n"
              "Copy .tasks/template.md to .tasks/current_task.md and fill it in.",
              file=sys.stderr)
        return 1
    spec = task_path.read_text(encoding="utf-8", errors="replace")

    agent_argv = parse_agent_cmd(args.agent_cmd)
    require_tests = not args.no_require_tests
    max_iter = args.iterations
    heal_limit = args.self_heal
    if max_iter < 1 or heal_limit < 1:
        parser.error("--iterations and --self-heal must be >= 1")

    tmp_dir = root / ".tasks" / ".tmp"
    if args.keep:
        tmp_dir.mkdir(parents=True, exist_ok=True)

    print(f"== ADD TDD LOOP ==")
    print(f"  task        : {task_path}")
    print(f"  agent cmd   : {' '.join(agent_argv)}")
    print(f"  iterations  : {max_iter}  (1 attempt + {max_iter - 1} self-heal)")
    print(f"  circuit     : max {heal_limit} fix attempts")
    print(f"  require-tests: {require_tests}")

    # Optional baseline validation: report current repo state to the agent.
    baseline_fail: str | None = None
    if not args.no_baseline:
        print("\n== baseline validation (current repo state) ==", flush=True)
        ok, stage, _key, tail = validate(root, require_tests, args.timeout)
        if not ok:
            baseline_fail = f"Stage '{stage}' failed.\n" + tail
            print("== baseline FAILED — agent will see the current broken state ==", flush=True)

    attempt = 0
    failed = 0
    feedback: str | None = None
    last_tail = ""
    last_stage = ""

    while attempt < max_iter:
        attempt += 1
        prompt = compose_prompt(attempt, spec, max_iter, feedback, baseline_fail)
        if args.keep:
            prompt_file = tmp_dir / f"prompt_{attempt:02d}.md"
            prompt_file.write_text(prompt, encoding="utf-8")
            print(f"\n== prompt saved: {prompt_file}", flush=True)

        print(f"\n== [{attempt}/{max_iter}] invoking agent: {' '.join(agent_argv)}", flush=True)
        before_state = repo_state(root)
        agent_rc, agent_out = run_cmd(build_agent_argv(agent_argv, prompt), root, args.timeout)
        print(f"== agent exit code: {agent_rc}", flush=True)
        if agent_rc != 0 or not args.allow_noop:
            print("-- agent output tail (last 2000 chars):", flush=True)
            print(agent_out[-2000:], flush=True)
        after_state = repo_state(root)
        if agent_rc == 0 and not args.allow_noop and before_state and after_state == before_state:
            print("-- agent made NO changes on disk; treated as failed attempt (no-op)", flush=True)
            failed += 1
            last_stage, last_tail = "NOOP", "Agent run produced no file changes. It must "
            last_tail += "create/modify files per the task spec (see Affected Files)."
        elif agent_rc != 0:
            failed += 1
            last_stage = "AGENT"
            last_tail = agent_out[-STACKTRACE_TAIL:]
            print("-- agent run failed; treated as failed attempt", flush=True)
        else:
            ok, stage, _key, tail = validate(root, require_tests, args.timeout)
            if ok:
                print("\n== ALL VALIDATION STAGES PASSED ==", flush=True)
                print(f"== task '{task_path.name}' completed on attempt {attempt}", flush=True)
                return 0
            failed += 1
            last_stage, last_tail = stage, tail
            print(f"-- validation failed after attempt {attempt} ({stage})", flush=True)

        if attempt >= max_iter or failed >= heal_limit:
            print_circuit_breaker(attempt, failed, last_stage, last_tail)
            return 1

        feedback = FEEDBACK_BLOCK.format(
            failed=failed,
            max_total=max_iter,
            stage=last_stage,
            cmd=" ".join(agent_argv),
            attempt=attempt,
            tail_len=STACKTRACE_TAIL,
            tail=last_tail,
        )

    # Unreachable, defensive.
    print_circuit_breaker(attempt, failed, last_stage, last_tail)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())