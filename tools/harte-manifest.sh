#!/usr/bin/env bash
# Records the git blob hash of every Harte test file at the pinned commit,
# from one call to GitHub's tree API, so the tests can check each file they
# download without anyone downloading 5 GB first.
#
#   tools/harte-manifest.sh
set -euo pipefail

commit=2f6980a2d95757486c7bee24355c360e40e2a224
root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/tests/Dbhq.Cpu6502.Tests/Harte/harte.manifest"

gh api "repos/dbhq-uk/65x02/git/trees/${commit}?recursive=1" --jq '
  if .truncated then error("tree listing truncated") else . end
  | .tree[]
  | select(.type == "blob" and (.path | test("^(6502|nes6502|synertek65c02|rockwell65c02|wdc65c02)/v1/[0-9a-f]{2}\\.json$")))
  | .sha + " " + .path' | sort -k2 > "$out"

lines=$(wc -l < "$out")
[ "$lines" -eq 1280 ] || { echo "expected 1280 files, got $lines" >&2; exit 1; }
echo "$lines files written to $out"
