#!/usr/bin/env bash
# Runs the NES inside model's measurements, in order, from the inputs.
#
#   NES_MODEL_INPUTS=<folder of inputs> tools/nes-model/run-board.sh
#
# The folder holds the inputs under the names data/sources.json gives (the
# README says how to fetch each by hand), and every script checks an input's
# SHA-256 before it reads it. PYTHON picks the interpreter, for a venv:
#   PYTHON=/tmp/nesvenv/bin/python tools/nes-model/run-board.sh
# A script that crosses one of the plan's STOP thresholds exits 3, and the
# run stops there.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
py="${PYTHON:-python3}"
: "${NES_MODEL_INPUTS:?set NES_MODEL_INPUTS to the folder of inputs (see tools/nes-model/README.md)}"
cd "$here"

"$py" verify.py I1-front I1-back I2
"$py" board_frame.py        # data/frame.json: the scan's scale, turn, outline and holes
"$py" board_register.py     # data/registration.json: the solder side registered; the pads, drills and footprints
"$py" board_trace.py --map-ppm 10   # data/copper.json and the track map: both copper faces, the print, the checks (10 px/mm chosen from --look on 5 Oct 2026)
