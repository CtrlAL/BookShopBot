#!/usr/bin/env sh
# pre-commit verification for BookShopBot (ADD framework).
#
# Version-controlled hook logic. The native git hook (.git/hooks/pre-commit)
# delegates to this script, so the checks live in the repo and travel with it.
#
# Blocks any `git commit` when the deterministic verification pipeline fails:
#   1. dotnet format --verify-no-changes   (linter / formatting)
#   2. dotnet build ... -warnaserror       (static typing / compile)
#   3. dotnet test ...                     (affected/full test suite)
#
# Emergency override (deliberate, documented): git commit -n  or:
#   ADD_SKIP_PRECOMMIT=1 git commit ...
set -u

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "[pre-commit] not a git repo"; exit 1; }
cd "$ROOT" || exit 1

if [ "${ADD_SKIP_PRECOMMIT:-0}" = "1" ]; then
  echo "[pre-commit] SKIPPED (ADD_SKIP_PRECOMMIT=1)"
  exit 0
fi

SLN="BookShop/BookShop.sln"
if [ ! -f "$SLN" ]; then
  echo "[pre-commit] solution not found: $SLN"
  exit 1
fi

FAIL=0
run_stage() {
  local label="$1"; shift
  echo ""
  echo "[pre-commit] $label: $*"
  "$@"
  local rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "[pre-commit] FAILED: $label (exit=$rc)"
    FAIL=1
  else
    echo "[pre-commit] OK: $label"
  fi
}

# 1. Linter / formatting
run_stage "dotnet format" dotnet format "$SLN" --verify-no-changes --verbosity minimal

# 2. Static typing / compile (C# compiler warnings escalated to errors;
#    known NuGet/package warnings deliberately non-blocking)
run_stage "dotnet build" dotnet build "$SLN" --no-restore -p:WarningsAsErrors=CS*

# 3. Tests (reuse the build above; no re-build)
run_stage "dotnet test" dotnet test "$SLN" --no-build --verbosity minimal

echo ""
if [ "$FAIL" -ne 0 ]; then
  echo "[pre-commit] BLOCKED: one or more verification stages failed."
  echo "[pre-commit] Fix the errors above, then commit again."
  echo "[pre-commit] Emergency bypass (use consciously): git commit -n"
  exit 1
fi

echo "[pre-commit] All verification stages passed."
exit 0