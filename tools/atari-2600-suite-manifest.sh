#!/usr/bin/env bash
# Records the git blob hash of every file of the Atari 2600 test suite the tests
# may read, from one call to GitHub's tree API per fork at its pinned commit, so
# each download can be checked without anything being committed (AGENTS.md rule 3).
#
#   dbhq-uk/missingno-vcs-tests: every .asm under roms/ (the tests' sources and
#     headers) and every _ntsc.png and _pal.png beside them (the reference pictures).
#   dbhq-uk/missingno: every _ntsc.a26, _pal.a26 and _secam.a26 under
#     crates/systems/vcs/tests/accuracy/roms/ (the built binaries; nothing else there is read).
#
# Each line is "<git blob sha> <fork> <path in the fork>", sorted by path. The
# manifest's own SHA-256 goes in Pins.Atari2600SuiteManifestSha256, so Pins.cs pins
# every file. Run it again only when a commit below changes, and update Pins.cs.
#
#   tools/atari-2600-suite-manifest.sh
set -euo pipefail

suite_commit=891c079976a2f68033ca4af2fe2fd85aaf1906c9
binaries_commit=a4159f47ce7ef6f9c3d5402a8c58b18940045adc
root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/tests/Dbhq.Cpu6502.TestSupport/atari-2600-suite.manifest"

# The commits must be the ones Pins.cs names, so the manifest and the pins cannot drift apart.
pins="$root/tests/Dbhq.Cpu6502.TestSupport/Pins.cs"
grep -q "Atari2600SuiteCommit = \"$suite_commit\"" "$pins" || { echo "Pins.Atari2600SuiteCommit is not $suite_commit" >&2; exit 1; }
grep -q "Atari2600BinariesCommit = \"$binaries_commit\"" "$pins" || { echo "Pins.Atari2600BinariesCommit is not $binaries_commit" >&2; exit 1; }

# tree <fork> <commit> <jq path test>: one line per matching blob.
tree() {
  gh api "repos/dbhq-uk/$1/git/trees/$2?recursive=1" --jq "
    if .truncated then error(\"tree listing truncated\") else . end
    | .tree[]
    | select(.type == \"blob\" and (.path | test(\"$3\")))
    | .sha + \" $1 \" + .path"
}

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT
tree missingno-vcs-tests "$suite_commit" '^roms/[a-z0-9-]+/[a-z0-9-]+(\\.asm|_ntsc\\.png|_pal\\.png)$' > "$tmp"
tree missingno "$binaries_commit" '^crates/systems/vcs/tests/accuracy/roms/[a-z0-9-]+/[a-z0-9-]+_(ntsc|pal|secam)\\.a26$' >> "$tmp"
LC_ALL=C sort -k3,3 "$tmp" > "$out"

suite=$(grep -c ' missingno-vcs-tests ' "$out" || true)
binaries=$(grep -c ' missingno ' "$out" || true)
[ "$suite" -gt 0 ] || { echo "no files from dbhq-uk/missingno-vcs-tests" >&2; exit 1; }
[ "$binaries" -gt 0 ] || { echo "no files from dbhq-uk/missingno" >&2; exit 1; }
echo "$suite files from dbhq-uk/missingno-vcs-tests and $binaries from dbhq-uk/missingno written to $out"
echo "its SHA-256, for Pins.Atari2600SuiteManifestSha256: $(sha256sum "$out" | cut -d' ' -f1)"
