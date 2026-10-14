#!/usr/bin/env bash
# Shows each test in SuiteManifestTests able to fail (plan task 1): plants one fault at a time in a
# scratch copy of the repository (the manifest, Pins.cs or Atari2600Suite.cs), runs the Atari 2600
# test project, prints what failed and puts the file back. Never run it on the working tree.
#
#   tools/probes/atari-2600/manifest-mutations.sh <scratch copy of the repository>
set -u
dst="${1:?give a scratch copy of the repository}"
m="$dst/tests/Dbhq.Cpu6502.TestSupport/atari-2600-suite.manifest"
p="$dst/tests/Dbhq.Cpu6502.TestSupport/Pins.cs"
s="$dst/tests/Dbhq.Cpu6502.TestSupport/Atari2600Suite.cs"
for f in "$m" "$p" "$s"; do cp "$f" "$f.orig"; done
repin() { sed -i "s/Atari2600SuiteManifestSha256 = \"[0-9a-f]*\"/Atari2600SuiteManifestSha256 = \"$(sha256sum "$m" | cut -d' ' -f1)\"/" "$p"; }
run() {
  echo "=== $1"
  (cd "$dst" && dotnet test tests/Dbhq.Machines.Atari2600.Tests 2>&1 | grep -E "^\s+Failed |Passed!|Failed!|does not match|error" | head -8)
  for f in "$m" "$p" "$s"; do cp "$f.orig" "$f"; done
}
sanity='\( missingno crates\/systems\/vcs\/tests\/accuracy\/roms\/harness\/sanity_ntsc.a26\)$'
sed -i "s/^[0-9a-f]\{40\}$sanity/0000000000000000000000000000000000000000\1/" "$m"
run "a blob hash corrupted, Pins.cs left as it was"
sed -i "s/^[0-9a-f]\{40\}$sanity/0000000000000000000000000000000000000000\1/" "$m"; repin
run "a blob hash corrupted and the manifest pinned again"
sed -i '/roms\/cpu\/floating-bus_secam.a26$/d' "$m"; repin
run "a binary dropped"
sed -i '/roms\/cpu\/floating-bus.asm$/d' "$m"; repin
run "a source dropped"
sed -i '/roms\/tia-render\/colors_pal.png$/d' "$m"; repin
run "a PAL picture dropped"
sed -i 's/ missingno crates\/systems\/vcs\/tests\/accuracy\/roms\/riot\/ram_pal.a26$/ missingno-vcs-tests roms\/riot\/ram_pal.a26/' "$m"; repin
run "a binary claimed by the suite's fork"
sed -i 's/^[0-9a-f]\{40\}\( missingno-vcs-tests roms\/riot\/ram.asm\)$/abc\1/' "$m"; repin
run "a short blob hash"
sed -i '1{h;d};2{G}' "$m"; repin
run "the manifest's first two lines swapped"
sed -i 's/throw new ArgumentException(\$"{name} is not in {ManifestPath}", nameof(name));/entry = Manifest.Value.Values.First();/' "$s"
run "an unknown name read as the first entry"
sed -i 's/return File.ReadAllBytes(PinnedFiles.Fetch(url, local, PinnedFiles.GitBlob(entry.BlobSha)));/return [.. File.ReadAllBytes(PinnedFiles.Fetch(url, local, PinnedFiles.GitBlob(entry.BlobSha))).Reverse()];/' "$s"
run "the bytes returned reversed"
for f in "$m" "$p" "$s"; do rm -f "$f.orig"; done
