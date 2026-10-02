#!/usr/bin/env bash
# Runs the whole KIM-1 model analysis, in order, from the full-size originals.
#
#   tools/kim1-model/run-all.sh <folder of originals> <the replica's kim-1.kicad_pcb>
#
# The folder must hold the originals under the names their URLs give them
# (data/sources.json lists each URL and its SHA-256, and every script checks
# the hash before it reads a file). PYTHON picks the interpreter, for a venv:
#   PYTHON=/path/to/venv/bin/python tools/kim1-model/run-all.sh ...
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
originals="$1"
replica="$2"
py="${PYTHON:-python3}"
cd "$here"

ORIGINALS=(
  --original "bolo-top=$originals/MOS_KIM-1_IMG_4211_cropped.jpg"
  --original "bolo-end=$originals/MOS_KIM-1_IMG_4210.jpg"
  --original "bolo-oblique=$originals/MOS_KIM-1_IMG_4209.jpg"
  --original "rev-b-front=$originals/20160317_143332_HDR-2.jpg"
  --original "rev-b-back=$originals/20160317_143341_HDR-2.jpg"
)

step() { echo; echo "== $*"; }
step extract_kicad.py; "$py" extract_kicad.py "$replica"
step register.py; "$py" register.py "${ORIGINALS[@]}"
step rectify.py; "$py" rectify.py "${ORIGINALS[@]}"
step trace.py; "$py" trace.py
step fuse.py; "$py" fuse.py
step heights.py; "$py" heights.py
step layout.py; "$py" layout.py
step results.py; "$py" results.py
